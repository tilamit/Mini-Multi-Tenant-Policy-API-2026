namespace PolicyApi.Tenancy;

/// <summary>
/// Holds the tenant identity for the current request scope (Layer 2 of the isolation strategy).
/// Fail-closed: <see cref="TenantId"/> throws if read before a tenant has been resolved, so the
/// system's failure mode is "no data", never "someone else's data". It must never default to 0,
/// null, or "all tenants".
/// </summary>
public interface ITenantContext
{
    int TenantId { get; }
    bool IsResolved { get; }
    void Resolve(int tenantId);
}
