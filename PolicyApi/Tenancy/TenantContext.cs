namespace PolicyApi.Tenancy;

/// <summary>Scoped per request. See <see cref="ITenantContext"/> for the fail-closed contract.</summary>
public sealed class TenantContext : ITenantContext
{
    private int? _tenantId;

    public bool IsResolved => _tenantId.HasValue;

    public int TenantId => _tenantId ?? throw new InvalidOperationException(
        "Tenant context has not been resolved; no tenant-scoped data may be accessed. " +
        "This fail-closed guard ensures the failure mode is no data, never another tenant's data.");

    public void Resolve(int tenantId)
    {
        if (_tenantId.HasValue && _tenantId.Value != tenantId)
            throw new InvalidOperationException("Tenant context has already been resolved to a different tenant.");
        _tenantId = tenantId;
    }
}
