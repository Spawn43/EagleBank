using EagleBank.Api.DTOs.Accounts;
using EagleBank.Domain.DTOs;

namespace EagleBank.Api.Mappings;

public static class BankAccountMappings
{
    public static BankAccountResponse ToResponse(this BankAccountDto dto) => new()
    {
        AccountNumber = dto.AccountNumber,
        SortCode = dto.SortCode,
        Name = dto.Name,
        AccountType = dto.AccountType,
        Balance = dto.Balance,
        Currency = dto.Currency,
        CreatedTimestamp = dto.CreatedTimestamp,
        UpdatedTimestamp = dto.UpdatedTimestamp
    };
}
