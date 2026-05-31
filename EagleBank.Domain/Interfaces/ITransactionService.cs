using EagleBank.Domain.DTOs;
using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface ITransactionService
{
    Task<TransactionDto> CreateTransactionAsync(string accountNumber, decimal amount, string currency, TransactionType type, string? reference, string authenticatedUserId);
    Task<IEnumerable<TransactionDto>> ListTransactionsAsync(string accountNumber, string authenticatedUserId);
    Task<TransactionDto> GetTransactionAsync(string accountNumber, string transactionId, string authenticatedUserId);
}
