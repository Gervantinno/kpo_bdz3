namespace OrdersService.Models;

public enum OrderStatus
{
    Created,
    PaymentPending,
    PaymentProcessed,
    PaymentFailed
}