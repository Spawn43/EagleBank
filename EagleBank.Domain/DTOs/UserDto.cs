using EagleBank.Domain.Models;

namespace EagleBank.Domain.DTOs;

public record UserDto(
    string Id,
    string Name,
    Address Address,
    string PhoneNumber,
    string Email,
    DateTime CreatedTimestamp,
    DateTime UpdatedTimestamp
);
