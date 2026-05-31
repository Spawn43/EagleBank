using EagleBank.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EagleBank.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().OwnsOne(u => u.Address);
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<BankAccount>().HasKey(a => a.AccountNumber);

        modelBuilder.Entity<Transaction>().HasKey(t => t.Id);
        modelBuilder.Entity<Transaction>()
            .HasOne<BankAccount>()
            .WithMany()
            .HasForeignKey(t => t.AccountNumber);
    }
}
