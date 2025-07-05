namespace Produtor;

public class Order
{
    public long Id { get; private set; }
    public DateTime CreatedDate { get; }
    public DateTime LastUpdated { get; private set; }
    public long Amount { get; private set; }

    public Order(long id, long amount)
    {
        Id = id;
        CreatedDate = DateTime.UtcNow;
        LastUpdated = CreatedDate;
        Amount = amount;
    }

    public void UpdateOrder(long amount)
    {
        Amount = amount;
        LastUpdated = DateTime.UtcNow;
    }
}
