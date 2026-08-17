using Microsoft.EntityFrameworkCore;
using PolicyApi.Contracts;
using PolicyApi.Data;
using PolicyApi.Time;

namespace PolicyApi.Services;

/// <summary>
/// Read-only tenant-scoped queries. Every query below runs through <see cref="PolicyDbContext"/>,
/// so the global query filters guarantee it can only ever see the calling tenant's data. There are
/// deliberately no per-method tenant checks: isolation lives in the data layer, not here.
/// </summary>
public sealed class PolicyService
{
    // A sane upper bound on the expiring-policies window; rejects absurd/abusive inputs.
    public const int MaxWithinDays = 366;

    private readonly PolicyDbContext _db;
    private readonly IClock _clock;

    public PolicyService(PolicyDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// The policy for a customer, scoped to the calling tenant. Returns null when the customer does
    /// not exist in this tenant (a valid id from another tenant is invisible here) or has no policy;
    /// both map to 404. Assumption: the brief's singular "/policy" means one policy per customer; if
    /// several exist we return the soonest-expiring. Stated in the README.
    /// </summary>
    public async Task<PolicyResponse?> GetPolicyForCustomerAsync(int customerId, CancellationToken ct = default)
    {
        return await _db.Policies
            .Where(p => p.CustomerId == customerId)
            .OrderBy(p => p.ExpirationDate)
            .Select(p => new PolicyResponse(p.Id, p.CustomerId, p.PolicyNumber, p.ExpirationDate, p.PremiumAmount))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Policies expiring within the window, tenant-scoped. Window is today (inclusive) through
    /// today + <paramref name="withinDays"/> (inclusive); already-expired policies are excluded
    /// because "expiring" is future-facing. "Today" is a single fixed UTC value per request.
    /// </summary>
    public async Task<IReadOnlyList<ExpiringPolicyResponse>> GetExpiringPoliciesAsync(int withinDays, CancellationToken ct = default)
    {
        var today = _clock.UtcToday;
        var windowEnd = today.AddDays(withinDays);

        return await _db.Policies
            .Where(p => p.ExpirationDate >= today && p.ExpirationDate <= windowEnd)
            .OrderBy(p => p.ExpirationDate)
            .Select(p => new ExpiringPolicyResponse(p.Id, p.CustomerId, p.PolicyNumber, p.ExpirationDate))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Bonus endpoint. Deliberately deterministic: the expiration date is returned verbatim from
    /// storage or not at all (404). There is no inference, computation, or approximation layer.
    /// </summary>
    public async Task<ExpirationDateResponse?> GetExpirationDateAsync(string policyNumber, CancellationToken ct = default)
    {
        return await _db.Policies
            .Where(p => p.PolicyNumber == policyNumber)
            .Select(p => new ExpirationDateResponse(p.PolicyNumber, p.ExpirationDate))
            .FirstOrDefaultAsync(ct);
    }
}
