namespace EagleBank.Domain.Models;

public class Transaction
{
    public string Id { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public string? Reference { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedTimestamp { get; set; }
}
