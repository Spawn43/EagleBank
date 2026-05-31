using EagleBank.Api.DTOs.Transactions;
using EagleBank.Domain.DTOs;

namespace EagleBank.Api.Mappings;

public static class TransactionMappings
{
    public static TransactionResponse ToResponse(this TransactionDto dto) => new()
    {
        Id = dto.Id,
        Amount = dto.Amount,
        Currency = dto.Currency,
        Type = dto.Type,
        Reference = dto.Reference,
        UserId = dto.UserId,
        CreatedTimestamp = dto.CreatedTimestamp
    };
}
