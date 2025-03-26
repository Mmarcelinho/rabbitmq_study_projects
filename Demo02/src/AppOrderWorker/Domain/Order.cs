namespace AppOrderWorker.Domain;

public sealed class Order
{
    public int OrderNumber { get; set; }

    public string ItemName { get; set; }

    public decimal Price { get; set; }
}