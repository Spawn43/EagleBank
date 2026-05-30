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
    // Pre-computed sentinel used when the email is not found. Without this,
    // the unknown-email path returns in ~1ms (no BCrypt work) while the
    // wrong-password path takes ~100-300ms, leaking which emails are registered
    // via response-time measurement.
    private const string DummyHash = "$2b$11$invalidhashvaluethatwillnevermatc";

    public async Task<AuthDto?> AuthenticateAsync(string email, string password)
    {
        logger.LogInformation("Authentication attempt received");

        var user = await userRepository.GetByEmailAsync(email);

        // Always run BCrypt regardless of whether the user exists so both failure
        // paths take the same time, preventing user enumeration via timing.
        var hashToVerify = user?.PasswordHash ?? DummyHash;
        var passwordValid = passwordHasher.Verify(password, hashToVerify);

        if (user is null || !passwordValid)
        {
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
