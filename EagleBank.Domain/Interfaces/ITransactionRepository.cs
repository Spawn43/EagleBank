using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction> CreateAsync(Transaction transaction);
    Task<IEnumerable<Transaction>> GetByAccountNumberAsync(string accountNumber);
    Task<Transaction?> GetByIdAsync(string transactionId);
}
