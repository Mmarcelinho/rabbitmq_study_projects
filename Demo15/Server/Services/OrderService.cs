namespace Server.Services;

public sealed class OrderService
{
    public static OrderStatus OnStore(decimal amount)
    {
        return (amount < 0 || amount > 1000) ? OrderStatus.Declined : OrderStatus.Aproved;
    }
}