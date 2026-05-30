using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EagleBank.Domain.Services;

public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthDto?> AuthenticateAsync(string email, string password)
    {
        logger.LogInformation("Authentication attempt received");

        var user = await userRepository.GetByEmailAsync(email);

        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            // Deliberately the same log and response for both cases to prevent user enumeration
            logger.LogWarning("Authentication failed - invalid credentials");
            return null;
        }

        var token = tokenGenerator.GenerateToken(ToDto(user));

        logger.LogInformation("Authentication successful for user {UserId}", user.Id);

        return new AuthDto(token);
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.Name,
        user.Address,
        user.PhoneNumber,
        user.Email,
        user.CreatedTimestamp,
        user.UpdatedTimestamp
    );
}
