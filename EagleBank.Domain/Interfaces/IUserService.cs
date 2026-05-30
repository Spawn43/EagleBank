using EagleBank.Domain.DTOs;
using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IUserService
{
    Task<UserDto> CreateUserAsync(string name, Address address, string phoneNumber, string email);
    Task<UserDto?> GetUserAsync(string id);
    Task<UserDto> UpdateUserAsync(string id, string? name, Address? address, string? phoneNumber, string? email);
    Task DeleteUserAsync(string id);
}
