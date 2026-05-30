using EagleBank.Domain.Models;

namespace EagleBank.Api.DTOs.Accounts;

public class BankAccountResponse
{
    public string AccountNumber { get; set; } = string.Empty;
    public string SortCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedTimestamp { get; set; }
    public DateTime UpdatedTimestamp { get; set; }
}
