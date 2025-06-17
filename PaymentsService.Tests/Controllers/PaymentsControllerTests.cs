using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PaymentsService.Controllers;
using PaymentsService.Data;
using PaymentsService.Models;
using Xunit;

namespace PaymentsService.Tests.Controllers;

public class WalletControllerSpecs
{
    private readonly PaymentsDbContext _db;
    private readonly PaymentsController _wallet;

    public WalletControllerSpecs()
    {
        var opts = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new PaymentsDbContext(opts);
        _db.Database.EnsureCreated();
        _wallet = new PaymentsController(_db);
    }

    [Fact]
    public async Task RegisterWallet_ForNewUser_ShouldReturnWalletWithZeroBalance()
    {
        // arrange
        var newUser = Guid.NewGuid();

        // act
        var response = await _wallet.CreateAccount(newUser);

        // assert
        var ok = Assert.IsType<OkObjectResult>(response);
        var wallet = Assert.IsType<Account>(ok.Value);
        Assert.Equal(newUser, wallet.UserId);
        Assert.True(wallet.Balance == 0);

        var dbWallet = await _db.Accounts.FirstOrDefaultAsync(x => x.UserId == newUser);
        Assert.NotNull(dbWallet);
        Assert.Equal(0, dbWallet.Balance);

        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "AccountCreated");
        Assert.NotNull(outbox);
    }

    [Fact]
    public async Task RegisterWallet_ForExistingUser_ShouldFail()
    {
        // arrange
        var user = Guid.NewGuid();
        _db.Accounts.Add(new Account { UserId = user, Balance = 0 });
        await _db.SaveChangesAsync();

        // act
        var result = await _wallet.CreateAccount(user);

        // assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TopUpWallet_ShouldIncreaseBalance_AndWriteOutbox()
    {
        // arrange
        var user = Guid.NewGuid();
        _db.Accounts.Add(new Account { UserId = user, Balance = 10 });
        await _db.SaveChangesAsync();

        var deposit = new PaymentsController.DepositRequest
        {
            UserId = user,
            Amount = 25
        };

        // act
        var result = await _wallet.Deposit(deposit);

        // assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var res = Assert.IsType<PaymentsController.DepositResult>(ok.Value);
        Assert.Equal(user, res.UserId);
        Assert.Equal(35m, res.Balance);

        var updated = await _db.Accounts.SingleAsync(x => x.UserId == user);
        Assert.Equal(35m, updated.Balance);

        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Type == "PaymentProcessed");
        Assert.NotNull(outbox);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task TopUpWallet_WithInvalidAmount_ShouldReturnBad(decimal sum)
    {
        // arrange
        var user = Guid.NewGuid();
        _db.Accounts.Add(new Account { UserId = user, Balance = 0 });
        await _db.SaveChangesAsync();

        var deposit = new PaymentsController.DepositRequest
        {
            UserId = user,
            Amount = sum
        };

        // act
        var result = await _wallet.Deposit(deposit);

        // assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetWalletBalance_ForExistingUser_ShouldReturnBalance()
    {
        // arrange
        var user = Guid.NewGuid();
        _db.Accounts.Add(new Account { UserId = user, Balance = 123 });
        await _db.SaveChangesAsync();

        // act
        var result = await _wallet.GetBalance(user);

        // assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var obj = Assert.IsAssignableFrom<object>(ok.Value);
        var json = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj));
        Assert.Equal(user, json.GetProperty("userId").GetGuid());
        Assert.Equal(123m, json.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task GetWalletBalance_ForUnknownUser_ShouldReturnNotFound()
    {
        // act
        var result = await _wallet.GetBalance(Guid.NewGuid());

        // assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TopUpWallet_ForMissingAccount_ShouldReturnNotFound()
    {
        // arrange
        var deposit = new PaymentsController.DepositRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 10
        };

        // act
        var result = await _wallet.Deposit(deposit);

        // assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TopUpWallet_WithZero_ShouldReturnBadRequest()
    {
        // arrange
        var user = Guid.NewGuid();
        _db.Accounts.Add(new Account { UserId = user, Balance = 1 });
        await _db.SaveChangesAsync();

        var deposit = new PaymentsController.DepositRequest
        {
            UserId = user,
            Amount = 0
        };

        // act
        var result = await _wallet.Deposit(deposit);

        // assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}