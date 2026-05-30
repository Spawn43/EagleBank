using EagleBank.Domain.DTOs;
using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IBankAccountService
{
    Task<BankAccountDto> CreateAccountAsync(string userId, string name, AccountType accountType);
    Task<IEnumerable<BankAccountDto>> ListAccountsAsync(string userId);
    Task<BankAccountDto?> GetAccountAsync(string accountNumber);
}
