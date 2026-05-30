using EagleBank.Domain.Models;

namespace EagleBank.Domain.Interfaces;

public interface IUserRepository
{
    Task<User> CreateAsync(User user);
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(User user);
}
