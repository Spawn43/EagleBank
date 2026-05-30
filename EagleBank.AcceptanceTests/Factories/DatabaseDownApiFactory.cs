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
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IUserRepository));

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddScoped<IUserRepository, AlwaysThrowingUserRepository>();
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
