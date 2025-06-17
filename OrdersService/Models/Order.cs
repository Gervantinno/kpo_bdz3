using System;
using System.ComponentModel.DataAnnotations;

namespace OrdersService.Models;

public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}