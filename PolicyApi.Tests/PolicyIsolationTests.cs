using PolicyApi.Services;

namespace PolicyApi.Tests;

public class PolicyIsolationTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);

    // 1. THE HEADLINE TEST. A request under Tenant A, using a real and valid customer id that
    //    belongs to Tenant B, must not see Tenant B's policy. This exercises the global query filter
    //    itself (not a hand-written Where), because the service has no tenant checks of its own.
    [Fact]
    public async Task GetPolicy_WithCustomerIdFromAnotherTenant_ReturnsNotFound()
    {
        using var db = new TestDatabase();
        var seed = TestSeed.Populate(db, Today);

        using var tenantAContext = db.Context(seed.TenantA);
        var service = new PolicyService(tenantAContext, new FixedClock(Today));

        // seed.CustomerB1 is a genuine, existing customer - just in the wrong tenant.
        var result = await service.GetPolicyForCustomerAsync(seed.CustomerB1);

        Assert.Null(result); // endpoint maps null -> 404, revealing nothing about Tenant B
    }

    // 2. Happy path: a customer's policy is returned correctly under its own tenant's context.
    [Fact]
    public async Task GetPolicy_ForOwnCustomer_ReturnsPolicy()
    {
        using var db = new TestDatabase();
        var seed = TestSeed.Populate(db, Today);

        using var tenantAContext = db.Context(seed.TenantA);
        var service = new PolicyService(tenantAContext, new FixedClock(Today));

        var result = await service.GetPolicyForCustomerAsync(seed.CustomerA1);

        Assert.NotNull(result);
        Assert.Equal(seed.CustomerA1, result!.CustomerId);
        Assert.Equal(seed.PolicyA1, result.PolicyNumber);
    }

    // 3. The expiring query is tenant-scoped: both tenants have a policy inside the window, but
    //    Tenant A must see only its own. Assert the exact expected set, not merely the count.
    [Fact]
    public async Task GetExpiringPolicies_ReturnsOnlyCallingTenantsPolicies()
    {
        using var db = new TestDatabase();
        var seed = TestSeed.Populate(db, Today);

        using var tenantAContext = db.Context(seed.TenantA);
        var service = new PolicyService(tenantAContext, new FixedClock(Today));

        var result = await service.GetExpiringPoliciesAsync(withinDays: 30);

        Assert.Equal(new[] { seed.PolicyA1 }, result.Select(p => p.PolicyNumber).ToArray());
    }

    // 4. Boundary behaviour: a policy expiring exactly on day N is included; one on day N+1 is not.
    //    Deterministic via the injected clock.
    [Fact]
    public async Task GetExpiringPolicies_IncludesDayN_ExcludesDayNPlus1()
    {
        const int n = 30;
        using var db = new TestDatabase();

        // Seed two policies for one tenant: one on the inclusive boundary, one just past it.
        using (var seedCtx = db.Context(tenantId: 0))
        {
            var tenant = new PolicyApi.Domain.Tenant { Name = "T" };
            seedCtx.Tenants.Add(tenant);
            seedCtx.SaveChanges();
            var customer = new PolicyApi.Domain.Customer { TenantId = tenant.Id, Name = "C" };
            seedCtx.Customers.Add(customer);
            seedCtx.SaveChanges();
            seedCtx.Policies.AddRange(
                new PolicyApi.Domain.Policy { CustomerId = customer.Id, PolicyNumber = "ON-BOUNDARY", ExpirationDate = Today.AddDays(n), PremiumAmount = 1m },
                new PolicyApi.Domain.Policy { CustomerId = customer.Id, PolicyNumber = "PAST-BOUNDARY", ExpirationDate = Today.AddDays(n + 1), PremiumAmount = 1m });
            seedCtx.SaveChanges();

            using var queryCtx = db.Context(tenant.Id);
            var service = new PolicyService(queryCtx, new FixedClock(Today));

            var result = await service.GetExpiringPoliciesAsync(withinDays: n);

            Assert.Equal(new[] { "ON-BOUNDARY" }, result.Select(p => p.PolicyNumber).ToArray());
        }
    }
}
