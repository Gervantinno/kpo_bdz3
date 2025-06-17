namespace OrdersService.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Data { get; set; }
    public DateTime Created { get; set; }
    public DateTime? ProcessedAt { get; set; }
}