using EagleBank.Domain.Models;

namespace EagleBank.Api.DTOs.Accounts;

public class UpdateBankAccountRequest
{
    public string? Name { get; set; }
    public AccountType? AccountType { get; set; }
}
