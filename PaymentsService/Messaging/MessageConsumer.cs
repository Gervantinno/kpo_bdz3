using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentsService.Messaging;

public class MessageConsumer : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName = "order_payments";
    private readonly ILogger<MessageConsumer> _logger;

    public MessageConsumer(IServiceScopeFactory scopeFactory, ILogger<MessageConsumer> logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var factory = new ConnectionFactory { HostName = "rabbitmq" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public void StartListening(CancellationToken cancellationToken = default)
    {
        _channel.QueueDeclare(_queueName, true, false, false);
        _channel.BasicQos(0, 1, false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                await HandleMessage(message, ea.DeliveryTag, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing message");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(_queueName, false, consumer);
    }

    public async Task HandleMessage(string message, ulong deliveryTag, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        var messageId = JsonDocument.Parse(message).RootElement.GetProperty("Id").GetGuid();

        if (await dbContext.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            _channel.BasicAck(deliveryTag, false);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var inboxMessage = new InboxMessage
            {
                Id = messageId,
                Data = message,
                MessageType = "OrderCreated",
                ReceivedAt = DateTime.UtcNow
            };
            dbContext.InboxMessages.Add(inboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);

            var orderData = JsonDocument.Parse(message).RootElement;
            var userId = orderData.GetProperty("UserId").GetGuid();
            var amount = orderData.GetProperty("Amount").GetDecimal();
            var orderId = orderData.GetProperty("OrderId").GetInt32();

            var account = await dbContext.Accounts
                .FromSqlRaw("SELECT * FROM \"Accounts\" WHERE \"UserId\" = {0} FOR UPDATE", userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null)
            {
                var failedOutboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "PaymentFailed",
                    Data = JsonSerializer.Serialize(new
                    {
                        OrderId = orderId,
                        Success = false,
                        Reason = "Account not found",
                        ProcessedAt = DateTime.UtcNow
                    }),
                    Created = DateTime.UtcNow
                };
                dbContext.OutboxMessages.Add(failedOutboxMessage);
            }
            else if (account.Balance < amount)
            {
                var failedOutboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "PaymentFailed",
                    Data = JsonSerializer.Serialize(new
                    {
                        OrderId = orderId,
                        Success = false,
                        Reason = "Insufficient funds",
                        ProcessedAt = DateTime.UtcNow
                    }),
                    Created = DateTime.UtcNow
                };
                dbContext.OutboxMessages.Add(failedOutboxMessage);
            }
            else
            {
                account.Balance -= amount;

                var successOutboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "PaymentProcessed",
                    Data = JsonSerializer.Serialize(new
                    {
                        OrderId = orderId,
                        Success = true,
                        ProcessedAt = DateTime.UtcNow
                    }),
                    Created = DateTime.UtcNow
                };
                dbContext.OutboxMessages.Add(successOutboxMessage);
            }

            inboxMessage.ProcessedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _channel.BasicAck(deliveryTag, false);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _channel.BasicNack(deliveryTag, false, true);
            throw;
        }
    }

    private class OrderCreatedEvent
    {
        public Guid Id { get; set; }
        public int OrderId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}