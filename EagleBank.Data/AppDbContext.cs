using Microsoft.EntityFrameworkCore;

namespace EagleBank.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
