namespace PolicyApi.Domain;

public class Customer
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public required string Name { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Policy> Policies { get; set; } = new List<Policy>();
}
