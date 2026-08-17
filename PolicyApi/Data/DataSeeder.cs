using PolicyApi.Domain;

namespace PolicyApi.Data;

/// <summary>
/// Seeds a small deterministic dataset so a reviewer can exercise every endpoint in under a minute:
/// 2 tenants, 2 customers each, and 4 policies with a spread of expiration dates that includes one
/// just inside and one just outside a 30-day window per tenant. Idempotent - it no-ops if data is
/// already present. Expiration dates are relative to today so the expiring endpoint is demonstrable.
/// </summary>
public static class DataSeeder
{
    public static void Seed(PolicyDbContext db)
    {
        // Tenants is not tenant-filtered, so this check is a reliable "already seeded?" gate.
        if (db.Tenants.Any())
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var acme = new Tenant { Name = "Acme Insurance" };
        var globex = new Tenant { Name = "Globex Assurance" };
        db.Tenants.AddRange(acme, globex);
        db.SaveChanges();

        var alice = new Customer { TenantId = acme.Id, Name = "Alice Adams" };
        var bob = new Customer { TenantId = acme.Id, Name = "Bob Barker" };
        var carol = new Customer { TenantId = globex.Id, Name = "Carol Chen" };
        var dave = new Customer { TenantId = globex.Id, Name = "Dave Diaz" };
        db.Customers.AddRange(alice, bob, carol, dave);
        db.SaveChanges();

        db.Policies.AddRange(
            // Acme: one just inside a 30-day window, one outside.
            new Policy { CustomerId = alice.Id, PolicyNumber = "ACME-1001", ExpirationDate = today.AddDays(29), PremiumAmount = 1200.00m },
            new Policy { CustomerId = bob.Id, PolicyNumber = "ACME-1002", ExpirationDate = today.AddDays(31), PremiumAmount = 850.50m },
            // Globex: one just inside, one outside.
            new Policy { CustomerId = carol.Id, PolicyNumber = "GLBX-2001", ExpirationDate = today.AddDays(10), PremiumAmount = 2400.75m },
            new Policy { CustomerId = dave.Id, PolicyNumber = "GLBX-2002", ExpirationDate = today.AddDays(90), PremiumAmount = 500.00m });
        db.SaveChanges();
    }
}
