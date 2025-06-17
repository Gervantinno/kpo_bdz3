using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace PaymentsService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsDbContext _context;

    public PaymentsController(PaymentsDbContext context)
    {
        _context = context;
    }

    
    [HttpPost("accounts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Создать платежный аккаунт",
        Description = "Создает новый платежный аккаунт для пользователя с нулевым балансом"
    )]
    public async Task<IActionResult> CreateAccount([FromBody] Guid userId)
    {
        if (await _context.Accounts.AnyAsync(a => a.UserId == userId))
            return BadRequest("Account already exists"); 

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var account = new Account 
            { 
                UserId = userId, 
                Balance = 0
            };
            _context.Accounts.Add(account);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "AccountCreated",
                Data = JsonSerializer.Serialize(new { UserId = userId }),
                Created = DateTime.UtcNow
            };
            _context.OutboxMessages.Add(outboxMessage);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        
            return Ok(account);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    
    [HttpPost("accounts/deposit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Пополнить баланс",
        Description = "Пополняет баланс указанного платежного аккаунта. Возвращает конфликт при проблемах с параллельным доступом"
    )]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive");

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == request.UserId);
        if (account == null)
            return NotFound();

        account.Balance += request.Amount;

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "PaymentProcessed",
            Data = JsonSerializer.Serialize(new 
            { 
                UserId = account.UserId, 
                Amount = request.Amount,
                NewBalance = account.Balance 
            }),
            Created = DateTime.UtcNow
        };
        _context.OutboxMessages.Add(outboxMessage);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("Concurrency conflict occurred");
        }

        return Ok(new DepositResult 
        { 
            UserId = account.UserId, 
            Balance = account.Balance 
        });
    }
    
    [HttpGet("accounts/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Получить баланс аккаунта",
        Description = "Возвращает информацию о балансе аккаунта пользователя"
    )]
    public async Task<IActionResult> GetBalance(Guid userId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account == null)
            return NotFound();

        return Ok(new { userId = account.UserId, balance = account.Balance });
    }

    public class DepositRequest
    {
        [SwaggerSchema(Description = "ID пользователя")]
        public Guid UserId { get; set; }
        [SwaggerSchema(Description = "Сумма пополнения")]
        public decimal Amount { get; set; }
    }

    public class DepositResult
    {
        [SwaggerSchema(Description = "ID пользователя")]
        public Guid UserId { get; set; }
        [SwaggerSchema(Description = "Текущий баланс после пополнения")]
        public decimal Balance { get; set; }
    }
}