using EagleBank.Domain.DTOs;

namespace EagleBank.Domain.Interfaces;

public interface ITokenGenerator
{
    string GenerateToken(UserDto user);
}
