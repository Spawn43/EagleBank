using System.ComponentModel.DataAnnotations;
using EagleBank.Domain.Models;

namespace EagleBank.Api.DTOs.Transactions;

public class CreateTransactionRequest
{
    [Range(0.01, 10000.00, ErrorMessage = "Amount must be between 0.01 and 10000.00")]
    public decimal Amount { get; set; }

    [Required]
    [AllowedValues("GBP", ErrorMessage = "Currency must be GBP")]
    public string? Currency { get; set; }

    [Required]
    public TransactionType? Type { get; set; }

    public string? Reference { get; set; }
}
