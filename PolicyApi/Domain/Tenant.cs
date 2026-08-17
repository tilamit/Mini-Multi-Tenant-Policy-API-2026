namespace PolicyApi.Domain;

public class Tenant
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
