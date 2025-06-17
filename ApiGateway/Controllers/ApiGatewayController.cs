using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api")]
public class ApiGatewayController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiGatewayController> _logger;

    public ApiGatewayController(IConfiguration configuration, ILogger<ApiGatewayController> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    [HttpPost("orders")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Создать новый заказ",
        Description = "Создает новый заказ для пользователя с указанной суммой"
    )]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var ordersServiceUrl = _configuration["Services:Orders"];
            var response = await _httpClient.PostAsJsonAsync(
                $"{ordersServiceUrl}/api/orders",
                request);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("orders/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Получить заказ по ID",
        Description = "Возвращает информацию о заказе по его идентификатору"
    )]
    public async Task<IActionResult> GetOrder(int id)
    {
        try
        {
            var ordersServiceUrl = _configuration["Services:Orders"];
            var response = await _httpClient.GetAsync(
                $"{ordersServiceUrl}/api/orders/{id}");

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("payments/accounts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Создать платежный аккаунт",
        Description = "Создает новый платежный аккаунт для пользователя"
    )]
    public async Task<IActionResult> CreateAccount([FromBody] Guid userId)
    {
        try
        {
            var paymentsServiceUrl = _configuration["Services:Payments"];
            var response = await _httpClient.PostAsJsonAsync(
                $"{paymentsServiceUrl}/api/payments/accounts",
                userId);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating account");
            return StatusCode(500, "Internal server error");
        }
    }
    

    [HttpGet("orders/user/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Получить заказы пользователя",
        Description = "Возвращает список всех заказов пользователя"
    )]
    public async Task<IActionResult> GetUserOrders(Guid userId)
    {
        try
        {
            var ordersServiceUrl = _configuration["Services:Orders"];
            var response = await _httpClient.GetAsync(
                $"{ordersServiceUrl}/api/orders/user/{userId}");

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user orders");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("payments/deposit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Пополнить баланс",
        Description = "Пополняет баланс указанного платежного аккаунта"
    )]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var paymentsServiceUrl = _configuration["Services:Payments"];
            var response = await _httpClient.PostAsJsonAsync(
                $"{paymentsServiceUrl}/api/payments/accounts/deposit",
                request);

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing deposit");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpGet("payments/accounts/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Получить баланс аккаунта",
        Description = "Возвращает информацию о балансе аккаунта пользователя"
    )]
    public async Task<IActionResult> GetBalance(Guid userId)
    {
        try
        {
            var paymentsServiceUrl = _configuration["Services:Payments"];
            var response = await _httpClient.GetAsync(
                $"{paymentsServiceUrl}/api/payments/accounts/{userId}");

            var content = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class CreateOrderRequest
{
    [SwaggerSchema(Description = "ID пользователя")]
    public Guid UserId { get; set; }
    [SwaggerSchema(Description = "Сумма заказа")]
    public decimal Amount { get; set; }
}

public class DepositRequest
{
    [SwaggerSchema(Description = "ID пользователя")]
    public Guid UserId { get; set; }
    [SwaggerSchema(Description = "Сумма пополнения")]
    public decimal Amount { get; set; }
}