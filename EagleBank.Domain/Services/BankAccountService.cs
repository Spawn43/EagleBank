using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EagleBank.Domain.Services;

public class BankAccountService(
    IBankAccountRepository bankAccountRepository,
    ILogger<BankAccountService> logger) : IBankAccountService
{
    public async Task<BankAccountDto> CreateAccountAsync(string userId, string name, AccountType accountType)
    {
        logger.LogInformation("Creating bank account for user {UserId}", userId);

        var account = new BankAccount
        {
            AccountNumber = $"01{Random.Shared.Next(0, 1_000_000):D6}",
            SortCode = "10-10-10",
            Name = name,
            AccountType = accountType,
            Balance = 0,
            Currency = "GBP",
            UserId = userId,
            CreatedTimestamp = DateTime.UtcNow,
            UpdatedTimestamp = DateTime.UtcNow
        };

        var created = await bankAccountRepository.CreateAsync(account);

        logger.LogInformation("Bank account created successfully {AccountNumber} for user {UserId}", created.AccountNumber, userId);

        return ToDto(created);
    }

    public async Task<IEnumerable<BankAccountDto>> ListAccountsAsync(string userId)
    {
        logger.LogInformation("Listing bank accounts for user {UserId}", userId);

        var accounts = await bankAccountRepository.GetByUserIdAsync(userId);
        var dtos = accounts.Select(ToDto).ToList();

        logger.LogInformation("Returning {Count} bank accounts for user {UserId}", dtos.Count, userId);

        return dtos;
    }

    public async Task<BankAccountDto?> GetAccountAsync(string accountNumber)
    {
        logger.LogInformation("Fetching bank account {AccountNumber}", accountNumber);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account not found {AccountNumber}", accountNumber);
            return null;
        }

        logger.LogInformation("Bank account fetched successfully {AccountNumber}", accountNumber);

        return ToDto(account);
    }

    private static BankAccountDto ToDto(BankAccount account) => new(
        account.AccountNumber,
        account.SortCode,
        account.Name,
        account.AccountType,
        account.Balance,
        account.Currency,
        account.UserId,
        account.CreatedTimestamp,
        account.UpdatedTimestamp
    );
}
