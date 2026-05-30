using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IBankAccountRepository
{
    Task<BankAccount> CreateAsync(BankAccount account);
    Task<BankAccount?> GetByAccountNumberAsync(string accountNumber);
    Task<IEnumerable<BankAccount>> GetByUserIdAsync(string userId);
}
