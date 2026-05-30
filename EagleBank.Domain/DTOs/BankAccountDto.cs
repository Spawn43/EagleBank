using EagleBank.Domain.Models;

namespace EagleBank.Domain.DTOs;

public record BankAccountDto(
    string AccountNumber,
    string SortCode,
    string Name,
    AccountType AccountType,
    decimal Balance,
    string Currency,
    string UserId,
    DateTime CreatedTimestamp,
    DateTime UpdatedTimestamp
);
