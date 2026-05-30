using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EagleBank.Data.Repositories;

public class BankAccountRepository(AppDbContext context) : IBankAccountRepository
{
    public async Task<BankAccount> CreateAsync(BankAccount account)
    {
        context.BankAccounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task<BankAccount?> GetByAccountNumberAsync(string accountNumber)
    {
        return await context.BankAccounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
    }

    public async Task<IEnumerable<BankAccount>> GetByUserIdAsync(string userId)
    {
        return await context.BankAccounts.Where(a => a.UserId == userId).ToListAsync();
    }
}
