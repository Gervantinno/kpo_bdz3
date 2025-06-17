namespace PaymentsService.Models;

public class InboxMessage
{
    public Guid Id { get; set; }
    public string MessageType { get; set; }
    public string Data { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}