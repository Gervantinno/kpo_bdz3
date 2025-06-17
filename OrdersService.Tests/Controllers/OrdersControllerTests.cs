using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrdersService.Controllers;
using OrdersService.Data;
using OrdersService.Models;
using Xunit;

namespace OrdersService.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly OrdersDbContext _context;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new OrdersDbContext(options);
        _controller = new OrdersController(_context);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreatedResult()
    {
        // Arrange
        var request = new OrdersController.OrderCreateRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var order = Assert.IsType<Order>(createdResult.Value);
        Assert.Equal(request.UserId, order.UserId);
        Assert.Equal(request.Amount, order.Amount);
        Assert.Equal(OrderStatus.PaymentPending, order.Status); // Исправленная строка

        var savedOrder = await _context.Orders.FindAsync(order.Id);
        Assert.NotNull(savedOrder);
        Assert.Equal(request.UserId, savedOrder.UserId);
        Assert.Equal(request.Amount, savedOrder.Amount);
        Assert.Equal(OrderStatus.PaymentPending, savedOrder.Status); // Добавлена проверка

        var outboxMessage = await _context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == "OrderCreated");
        Assert.NotNull(outboxMessage);
    }

    [Fact]
    public async Task GetOrderById_ExistingOrder_ReturnsOrder()
    {
        // Arrange
        var order = new Order
        {
            UserId = Guid.NewGuid(),
            Amount = 100,
            Status = OrderStatus.Created
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetOrderById(order.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOrder = Assert.IsType<Order>(okResult.Value);
        Assert.Equal(order.Id, returnedOrder.Id);
    }

    [Fact]
    public async Task GetOrderById_NonExistingOrder_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = 999;

        // Act
        var result = await _controller.GetOrderById(nonExistingId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsAllOrders()
    {
        // Arrange
        var orders = new List<Order>
        {
            new Order { UserId = Guid.NewGuid(), Amount = 100, Status = OrderStatus.Created },
            new Order { UserId = Guid.NewGuid(), Amount = 200, Status = OrderStatus.PaymentPending }
        };
        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllOrders();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOrders = Assert.IsType<List<Order>>(okResult.Value);
        Assert.Equal(2, returnedOrders.Count);
    }

    [Fact]
    public async Task GetUserOrders_ReturnsUserOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orders = new List<Order>
        {
            new Order { UserId = userId, Amount = 100, Status = OrderStatus.Created },
            new Order { UserId = userId, Amount = 200, Status = OrderStatus.PaymentPending },
            new Order { UserId = Guid.NewGuid(), Amount = 300, Status = OrderStatus.Created }
        };
        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUserOrders(userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOrders = Assert.IsType<List<Order>>(okResult.Value);
        Assert.Equal(2, returnedOrders.Count);
        Assert.All(returnedOrders, order => Assert.Equal(userId, order.UserId));
    }

    [Fact]
    public async Task GetUserOrders_NoOrders_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _controller.GetUserOrders(userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedOrders = Assert.IsType<List<Order>>(okResult.Value);
        Assert.Empty(returnedOrders);
    }
}