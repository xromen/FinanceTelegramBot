using Microsoft.EntityFrameworkCore;
using FinanceTelegramBot.Data.Entities;

namespace FinanceTelegramBot.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryKeyword> CategoryKeywords => Set<CategoryKeyword>();
    public DbSet<MoneyTransaction> MoneyTransactions => Set<MoneyTransaction>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<User> Users => Set<User>();
}
