using EagleBank.Domain.Models;

namespace EagleBank.Api.DTOs.Transactions;

public class TransactionResponse
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public string? Reference { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
}
