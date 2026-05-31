using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EagleBank.Data.Repositories;

public class TransactionRepository(AppDbContext context) : ITransactionRepository
{
    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    public async Task<IEnumerable<Transaction>> GetByAccountNumberAsync(string accountNumber)
    {
        return await context.Transactions
            .Where(t => t.AccountNumber == accountNumber)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdAsync(string transactionId)
    {
        return await context.Transactions.FirstOrDefaultAsync(t => t.Id == transactionId);
    }
}
