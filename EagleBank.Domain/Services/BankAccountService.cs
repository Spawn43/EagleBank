using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using EagleBank.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EagleBank.Domain.Services;

public class BankAccountService(
    IBankAccountRepository bankAccountRepository,
    ILogger<BankAccountService> logger,
    IOptions<BankAccountSettings> settings) : IBankAccountService
{
    public async Task<BankAccountDto> CreateAccountAsync(string userId, string name, AccountType accountType)
    {
        logger.LogInformation("Creating bank account for user {UserId}", userId);

        var accountNumber = await GenerateUniqueAccountNumberAsync(userId);

        var account = new BankAccount
        {
            AccountNumber = accountNumber,
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

    private async Task<string> GenerateUniqueAccountNumberAsync(string userId)
    {
        var maxRetries = settings.Value.AccountNumberMaxRetries;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var candidate = $"01{Random.Shared.Next(0, 1_000_000):D6}";

            if (!await bankAccountRepository.ExistsByAccountNumberAsync(candidate))
                return candidate;

            logger.LogWarning("Account number collision on attempt {Attempt} for user {UserId}", attempt + 1, userId);
        }

        logger.LogError("Failed to generate unique account number after {MaxRetries} attempts for user {UserId}", maxRetries, userId);
        throw new InvalidOperationException($"Failed to generate a unique account number after {maxRetries} attempts");
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

    public async Task<BankAccountDto> UpdateAccountAsync(string accountNumber, string? name, AccountType? accountType, string authenticatedUserId)
    {
        logger.LogInformation("Updating bank account {AccountNumber} for user {UserId}", accountNumber, authenticatedUserId);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account {AccountNumber} not found", accountNumber);
            throw new KeyNotFoundException($"Bank account {accountNumber} not found");
        }

        if (account.UserId != authenticatedUserId)
        {
            logger.LogWarning("User {UserId} attempted to update account {AccountNumber} owned by {OwnerId}",
                authenticatedUserId, accountNumber, account.UserId);
            throw new UnauthorizedAccessException();
        }

        if (name is not null) account.Name = name;
        if (accountType is not null) account.AccountType = accountType.Value;
        account.UpdatedTimestamp = DateTime.UtcNow;

        var updated = await bankAccountRepository.UpdateAsync(account);

        logger.LogInformation("Bank account {AccountNumber} updated successfully", accountNumber);

        return ToDto(updated);
    }

    public async Task DeleteAccountAsync(string accountNumber, string authenticatedUserId)
    {
        logger.LogInformation("Deleting bank account {AccountNumber} for user {UserId}", accountNumber, authenticatedUserId);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account {AccountNumber} not found", accountNumber);
            throw new KeyNotFoundException($"Bank account {accountNumber} not found");
        }

        if (account.UserId != authenticatedUserId)
        {
            logger.LogWarning("User {UserId} attempted to delete account {AccountNumber} owned by {OwnerId}",
                authenticatedUserId, accountNumber, account.UserId);
            throw new UnauthorizedAccessException();
        }

        await bankAccountRepository.DeleteAsync(account);

        logger.LogInformation("Bank account {AccountNumber} deleted successfully", accountNumber);
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
