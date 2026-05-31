using EagleBank.Domain.Models;

namespace EagleBank.Domain.DTOs;

public record TransactionDto(
    string Id,
    string AccountNumber,
    decimal Amount,
    string Currency,
    TransactionType Type,
    string? Reference,
    string UserId,
    DateTime CreatedTimestamp
);
