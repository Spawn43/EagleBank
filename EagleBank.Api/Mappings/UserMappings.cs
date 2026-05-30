using EagleBank.Api.DTOs.Users;
using EagleBank.Domain.DTOs;

namespace EagleBank.Api.Mappings;

public static class UserMappings
{
    public static UserResponse ToResponse(this UserDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Address = new AddressDto
        {
            Line1 = dto.Address.Line1,
            Line2 = dto.Address.Line2,
            Line3 = dto.Address.Line3,
            Town = dto.Address.Town,
            County = dto.Address.County,
            Postcode = dto.Address.Postcode
        },
        PhoneNumber = dto.PhoneNumber,
        Email = dto.Email,
        CreatedTimestamp = dto.CreatedTimestamp,
        UpdatedTimestamp = dto.UpdatedTimestamp
    };
}
