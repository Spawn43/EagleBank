using EagleBank.Domain.DTOs;

namespace EagleBank.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthDto?> AuthenticateAsync(string email, string password);
}
