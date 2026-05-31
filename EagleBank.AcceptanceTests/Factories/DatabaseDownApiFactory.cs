using EagleBank.Domain.Interfaces;
using EagleBank.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EagleBank.AcceptanceTests.Factories;

public class DatabaseDownApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var userRepo = services.SingleOrDefault(d => d.ServiceType == typeof(IUserRepository));
            if (userRepo != null) services.Remove(userRepo);
            services.AddScoped<IUserRepository, AlwaysThrowingUserRepository>();

            var accountRepo = services.SingleOrDefault(d => d.ServiceType == typeof(IBankAccountRepository));
            if (accountRepo != null) services.Remove(accountRepo);
            services.AddScoped<IBankAccountRepository, AlwaysThrowingBankAccountRepository>();
        });
    }
}

file class AlwaysThrowingUserRepository : IUserRepository
{
    private const string Message = "Database connection failed";

    public Task<User> CreateAsync(User user) => throw new Exception(Message);
    public Task<User?> GetByIdAsync(string id) => throw new Exception(Message);
    public Task<User?> GetByEmailAsync(string email) => throw new Exception(Message);
    public Task<User> UpdateAsync(User user) => throw new Exception(Message);
    public Task DeleteAsync(User user) => throw new Exception(Message);
}

file class AlwaysThrowingBankAccountRepository : IBankAccountRepository
{
    private const string Message = "Database connection failed";

    public Task<BankAccount> CreateAsync(BankAccount account) => throw new Exception(Message);
    public Task<bool> ExistsByAccountNumberAsync(string accountNumber) => throw new Exception(Message);
    public Task<BankAccount?> GetByAccountNumberAsync(string accountNumber) => throw new Exception(Message);
    public Task<IEnumerable<BankAccount>> GetByUserIdAsync(string userId) => throw new Exception(Message);
    public Task<BankAccount> UpdateAsync(BankAccount account) => throw new Exception(Message);
    public Task DeleteAsync(BankAccount account) => throw new Exception(Message);
}
