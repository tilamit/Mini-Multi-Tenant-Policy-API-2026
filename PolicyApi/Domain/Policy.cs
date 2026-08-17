namespace PolicyApi.Domain;

// Per the brief's schema, Policy has no TenantId. Tenant ownership is reached through the
// Customer relationship. Denormalizing TenantId onto Policy is discussed in the README as a
// production recommendation.
public class Policy
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public required string PolicyNumber { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }

    public Customer Customer { get; set; } = null!;
}
