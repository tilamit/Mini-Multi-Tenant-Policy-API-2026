using Microsoft.EntityFrameworkCore;
using PolicyApi.Domain;
using PolicyApi.Tenancy;

namespace PolicyApi.Data;

/// <summary>
/// Layer 3 of the isolation strategy: global query filters make tenant scoping the default for
/// every Customer and Policy query in the app, so a future careless <c>.Where()</c> cannot leak
/// another tenant's data.
/// </summary>
public sealed class PolicyDbContext : DbContext
{
    // Captured ONCE at construction. Query filters are compiled into the model, so the filter must
    // close over an immutable field rather than re-read a mutable property inside the lambda.
    // Reading TenantId here also enforces fail-closed: it throws if no tenant was resolved.
    private readonly int _tenantId;

    public PolicyDbContext(DbContextOptions<PolicyDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Policy> Policies => Set<Policy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            // Intentionally NOT tenant-filtered: the edge layer must be able to validate that a
            // supplied tenant id exists before any tenant context is trusted.
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.TenantId);
            e.HasOne(c => c.Tenant)
                .WithMany(t => t.Customers)
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(c => c.TenantId == _tenantId);
        });

        modelBuilder.Entity<Policy>(e =>
        {
            e.Property(p => p.PolicyNumber).IsRequired().HasMaxLength(100);
            e.HasIndex(p => p.PolicyNumber);
            e.Property(p => p.PremiumAmount).HasPrecision(18, 2);
            e.HasOne(p => p.Customer)
                .WithMany(c => c.Policies)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Policy has no TenantId (brief's schema), so the filter reaches through the required
            // Customer navigation. This is supported by EF Core; the generated SQL joins Customer
            // and compares its TenantId. Proven by the cross-tenant test in the test project.
            e.HasQueryFilter(p => p.Customer.TenantId == _tenantId);
        });
    }
}
