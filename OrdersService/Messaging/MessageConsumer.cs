using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Messaging;

public class MessageConsumer : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _queueName = "payment_results";
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
                await HandleMessage(message, ea.DeliveryTag);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing payment result");
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(_queueName, false, consumer);
    }

    private async Task HandleMessage(string message, ulong deliveryTag)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var paymentResult = JsonDocument.Parse(message).RootElement;
        var orderId = paymentResult.GetProperty("OrderId").GetInt32();
        var success = paymentResult.GetProperty("Success").GetBoolean();

        var order = await dbContext.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = success ? OrderStatus.PaymentProcessed : OrderStatus.PaymentFailed;
            await dbContext.SaveChangesAsync();
        }

        _channel.BasicAck(deliveryTag, false);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}