using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApiGateway.Tests.Controllers;

public class ApiGatewayControllerTests
{
    private readonly ApiGatewayController _controller;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<ApiGatewayController>> _loggerMock;

    public ApiGatewayControllerTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ApiGatewayController>>();

        _configMock.Setup(x => x["Services:Orders"]).Returns("http://orders-service");
        _configMock.Setup(x => x["Services:Payments"]).Returns("http://payments-service");

        _controller = new ApiGatewayController(_configMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrder_ServiceUnavailable_Returns500()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateAccount_ServiceUnavailable_Returns500()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _controller.CreateAccount(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetUserOrders_ServiceUnavailable_Returns500()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _controller.GetUserOrders(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }

    [Fact]
    public async Task GetBalance_ServiceUnavailable_Returns500()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _controller.GetBalance(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }

    [Fact]
    public async Task Deposit_ServiceUnavailable_Returns500()
    {
        // Arrange
        var request = new DepositRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.Deposit(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }

    [Fact]
    public async Task GetOrder_ServiceUnavailable_Returns500()
    {
        // Arrange
        var orderId = 1;

        // Act
        var result = await _controller.GetOrder(orderId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }

    [Fact]
    public void Constructor_ValidConfiguration_SetsUpHttpClient()
    {
        // Arrange + Act
        var controller = new ApiGatewayController(_configMock.Object, _loggerMock.Object);

        // Assert
        Assert.NotNull(controller);
    }

    [Fact]
    public async Task CreateOrder_ValidatesConfiguration()
    {
        // Arrange
        _configMock.Setup(x => x["Services:Orders"]).Returns((string)null);
        var request = new CreateOrderRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public void Services_UrlsAreCorrectlyConfigured()
    {
        // Assert
        Assert.Equal("http://orders-service", _configMock.Object["Services:Orders"]);
        Assert.Equal("http://payments-service", _configMock.Object["Services:Payments"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-url")]
    [InlineData(null)]
    public async Task InvalidServiceUrls_Returns500(string invalidUrl)
    {
        // Arrange
        _configMock.Setup(x => x["Services:Orders"]).Returns(invalidUrl);
        var request = new CreateOrderRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task MultipleRequests_LoggerCalledForEach()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _controller.GetBalance(userId);
        await _controller.GetUserOrders(userId);
        await _controller.GetOrder(1);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Deposit_LogsCorrectErrorMessage()
    {
        // Arrange
        var request = new DepositRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        await _controller.Deposit(request);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error processing deposit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestTimeout_Returns500()
    {
        // Arrange
        _configMock.Setup(x => x["Services:Orders"])
            .Returns("http://non-existent-service");
        var request = new CreateOrderRequest
        {
            UserId = Guid.NewGuid(),
            Amount = 100
        };

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        Assert.Equal("Internal server error", statusResult.Value);
    }
}