using EagleBank.Domain.DTOs;
using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IBankAccountService
{
    Task<BankAccountDto> CreateAccountAsync(string userId, string name, AccountType accountType);
    Task<IEnumerable<BankAccountDto>> ListAccountsAsync(string userId);
    Task<BankAccountDto?> GetAccountAsync(string accountNumber);
    Task<BankAccountDto> UpdateAccountAsync(string accountNumber, string? name, AccountType? accountType, string authenticatedUserId);
    Task DeleteAccountAsync(string accountNumber, string authenticatedUserId);
}
