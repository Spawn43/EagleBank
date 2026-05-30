using EagleBank.Domain.DTOs;
using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EagleBank.Domain.Services;

public class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto> CreateUserAsync(string name, Address address, string phoneNumber, string email)
    {
        logger.LogInformation("Creating new user");

        var user = new User
        {
            Id = $"usr-{Guid.NewGuid():N}",
            Name = name,
            Address = address,
            PhoneNumber = phoneNumber,
            Email = email,
            CreatedTimestamp = DateTime.UtcNow,
            UpdatedTimestamp = DateTime.UtcNow
        };

        var created = await userRepository.CreateAsync(user);

        logger.LogInformation("User created successfully {UserId}", created.Id);

        return ToDto(created);
    }

    public async Task<UserDto?> GetUserAsync(string id)
    {
        logger.LogInformation("Fetching user {UserId}", id);

        var user = await userRepository.GetByIdAsync(id);

        if (user is null)
        {
            logger.LogWarning("User not found {UserId}", id);
            return null;
        }

        logger.LogInformation("User fetched successfully {UserId}", id);

        return ToDto(user);
    }

    public async Task<UserDto> UpdateUserAsync(string id, string? name, Address? address, string? phoneNumber, string? email)
    {
        logger.LogInformation("Updating user {UserId}", id);

        var user = await userRepository.GetByIdAsync(id);

        if (user is null)
        {
            logger.LogWarning("Update failed, user not found {UserId}", id);
            throw new KeyNotFoundException($"User {id} not found");
        }

        if (name is not null) user.Name = name;
        if (address is not null) user.Address = address;
        if (phoneNumber is not null) user.PhoneNumber = phoneNumber;
        if (email is not null) user.Email = email;
        user.UpdatedTimestamp = DateTime.UtcNow;

        var updated = await userRepository.UpdateAsync(user);

        logger.LogInformation("User updated successfully {UserId}", id);

        return ToDto(updated);
    }

    public async Task DeleteUserAsync(string id)
    {
        logger.LogInformation("Deleting user {UserId}", id);

        var user = await userRepository.GetByIdAsync(id);

        if (user is null)
        {
            logger.LogWarning("Delete failed, user not found {UserId}", id);
            throw new KeyNotFoundException($"User {id} not found");
        }

        await userRepository.DeleteAsync(user);

        logger.LogInformation("User deleted successfully {UserId}", id);
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
