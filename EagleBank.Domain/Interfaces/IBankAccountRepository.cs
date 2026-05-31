using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IBankAccountRepository
{
    Task<BankAccount> CreateAsync(BankAccount account);
    Task<bool> ExistsByAccountNumberAsync(string accountNumber);
    Task<BankAccount?> GetByAccountNumberAsync(string accountNumber);
    Task<IEnumerable<BankAccount>> GetByUserIdAsync(string userId);
    Task<BankAccount> UpdateAsync(BankAccount account);
    Task DeleteAsync(BankAccount account);
}
