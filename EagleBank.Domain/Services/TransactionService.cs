using EagleBank.Domain.DTOs;
using EagleBank.Domain.Exceptions;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EagleBank.Domain.Services;

public class TransactionService(
    IBankAccountRepository bankAccountRepository,
    ITransactionRepository transactionRepository,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<TransactionDto> CreateTransactionAsync(
        string accountNumber, decimal amount, string currency, TransactionType type, string? reference, string authenticatedUserId)
    {
        logger.LogInformation("Creating {Type} transaction for account {AccountNumber}", type, accountNumber);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account {AccountNumber} not found", accountNumber);
            throw new KeyNotFoundException($"Bank account {accountNumber} not found");
        }

        if (account.UserId != authenticatedUserId)
        {
            logger.LogWarning("User {UserId} attempted to create transaction on account {AccountNumber} owned by {OwnerId}",
                authenticatedUserId, accountNumber, account.UserId);
            throw new UnauthorizedAccessException();
        }

        if (type == TransactionType.Withdrawal && account.Balance < amount)
        {
            logger.LogWarning("Insufficient funds for withdrawal on account {AccountNumber}: balance {Balance}, requested {Amount}",
                accountNumber, account.Balance, amount);
            throw new InsufficientFundsException();
        }

        account.Balance = type == TransactionType.Deposit
            ? account.Balance + amount
            : account.Balance - amount;
        account.UpdatedTimestamp = DateTime.UtcNow;

        await bankAccountRepository.UpdateAsync(account);

        var transaction = new Transaction
        {
            Id = $"tan-{Guid.NewGuid():N}",
            AccountNumber = accountNumber,
            Amount = amount,
            Currency = currency,
            Type = type,
            Reference = reference,
            UserId = authenticatedUserId,
            CreatedTimestamp = DateTime.UtcNow
        };

        var created = await transactionRepository.CreateAsync(transaction);

        logger.LogInformation("Transaction {TransactionId} created successfully on account {AccountNumber}", created.Id, accountNumber);

        return ToDto(created);
    }

    public async Task<IEnumerable<TransactionDto>> ListTransactionsAsync(string accountNumber, string authenticatedUserId)
    {
        logger.LogInformation("Listing transactions for account {AccountNumber}", accountNumber);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account {AccountNumber} not found", accountNumber);
            throw new KeyNotFoundException($"Bank account {accountNumber} not found");
        }

        if (account.UserId != authenticatedUserId)
        {
            logger.LogWarning("User {UserId} attempted to list transactions on account {AccountNumber} owned by {OwnerId}",
                authenticatedUserId, accountNumber, account.UserId);
            throw new UnauthorizedAccessException();
        }

        var transactions = await transactionRepository.GetByAccountNumberAsync(accountNumber);
        var dtos = transactions.Select(ToDto).ToList();

        logger.LogInformation("Returning {Count} transactions for account {AccountNumber}", dtos.Count, accountNumber);

        return dtos;
    }

    public async Task<TransactionDto> GetTransactionAsync(string accountNumber, string transactionId, string authenticatedUserId)
    {
        logger.LogInformation("Fetching transaction {TransactionId} on account {AccountNumber}", transactionId, accountNumber);

        var account = await bankAccountRepository.GetByAccountNumberAsync(accountNumber);

        if (account is null)
        {
            logger.LogWarning("Bank account {AccountNumber} not found", accountNumber);
            throw new KeyNotFoundException($"Bank account {accountNumber} not found");
        }

        if (account.UserId != authenticatedUserId)
        {
            logger.LogWarning("User {UserId} attempted to fetch transaction {TransactionId} on account {AccountNumber} owned by {OwnerId}",
                authenticatedUserId, transactionId, accountNumber, account.UserId);
            throw new UnauthorizedAccessException();
        }

        var transaction = await transactionRepository.GetByIdAsync(transactionId);

        if (transaction is null || transaction.AccountNumber != accountNumber)
        {
            logger.LogWarning("Transaction {TransactionId} not found on account {AccountNumber}", transactionId, accountNumber);
            throw new KeyNotFoundException($"Transaction {transactionId} not found");
        }

        logger.LogInformation("Transaction {TransactionId} fetched successfully", transactionId);

        return ToDto(transaction);
    }

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id,
        t.AccountNumber,
        t.Amount,
        t.Currency,
        t.Type,
        t.Reference,
        t.UserId,
        t.CreatedTimestamp
    );
}
