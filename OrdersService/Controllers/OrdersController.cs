using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrdersService.Data;
using OrdersService.Messaging;
using OrdersService.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace OrdersService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _context;

    public OrdersController(OrdersDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Создать заказ",
        Description = "Создает новый заказ и отправляет событие в очередь"
    )]
    public async Task<IActionResult> CreateOrder([FromBody] OrderCreateRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                UserId = request.UserId,
                Amount = request.Amount,
                Status = OrderStatus.PaymentPending
            };

            _context.Orders.Add(order);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "OrderCreated",
                Data = JsonSerializer.Serialize(new
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = order.UserId,
                    Amount = order.Amount
                }),
                Created = DateTime.UtcNow
            };
        
            _context.OutboxMessages.Add(outboxMessage);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Получить заказы пользователя",
        Description = "Возвращает список всех заказов конкретного пользователя"
    )]
    public async Task<IActionResult> GetUserOrders(Guid userId)
    {
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Получить все заказы",
        Description = "Возвращает список всех заказов в системе"
    )]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _context.Orders.ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Получить заказ по ID",
        Description = "Возвращает информацию о конкретном заказе"
    )]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }

    public class OrderCreateRequest
    {
        [SwaggerSchema(Description = "ID пользователя")]
        public Guid UserId { get; set; }
        [SwaggerSchema(Description = "Сумма заказа")]
        public decimal Amount { get; set; }
    }
}