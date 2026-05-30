using EagleBank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EagleBank.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().OwnsOne(u => u.Address);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<BankAccount>().HasKey(a => a.AccountNumber);
        modelBuilder.Entity<BankAccount>().HasIndex(a => a.AccountNumber).IsUnique();
    }
}
