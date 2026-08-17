using PolicyApi.Domain;

namespace PolicyApi.Tests;

/// <summary>
/// Seeds two tenants with a customer and a policy each. Inserts are not affected by query filters,
/// so everything is created through a single context; the real ids are captured and returned so the
/// cross-tenant test can request a genuine, valid id that belongs to the other tenant.
/// </summary>
public static class TestSeed
{
    public record Seeded(
        int TenantA, int TenantB,
        int CustomerA1, int CustomerB1,
        string PolicyA1, string PolicyB1);

    public static Seeded Populate(TestDatabase db, DateOnly today)
    {
        using var ctx = db.Context(tenantId: 0);

        var tenantA = new Tenant { Name = "Tenant A" };
        var tenantB = new Tenant { Name = "Tenant B" };
        ctx.Tenants.AddRange(tenantA, tenantB);
        ctx.SaveChanges();

        var customerA1 = new Customer { TenantId = tenantA.Id, Name = "A1" };
        var customerB1 = new Customer { TenantId = tenantB.Id, Name = "B1" };
        ctx.Customers.AddRange(customerA1, customerB1);
        ctx.SaveChanges();

        // Both policies expire inside a 30-day window so the tenant-scoping test is meaningful.
        ctx.Policies.AddRange(
            new Policy { CustomerId = customerA1.Id, PolicyNumber = "A-1001", ExpirationDate = today.AddDays(10), PremiumAmount = 100.00m },
            new Policy { CustomerId = customerB1.Id, PolicyNumber = "B-1001", ExpirationDate = today.AddDays(10), PremiumAmount = 200.00m });
        ctx.SaveChanges();

        return new Seeded(
            tenantA.Id, tenantB.Id,
            customerA1.Id, customerB1.Id,
            "A-1001", "B-1001");
    }
}
