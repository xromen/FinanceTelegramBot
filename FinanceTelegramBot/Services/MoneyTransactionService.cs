using Microsoft.EntityFrameworkCore;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Services;

public class MoneyTransactionService(ApplicationDbContext db)
{
    public async Task<MoneyTransaction> CreateAsync(MoneyTransaction transaction)
    {
        await db.MoneyTransactions.AddAsync(transaction);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(transaction.Id))!;
    }

    public async Task<MoneyTransaction?> GetByIdAsync(long id)
    {
        return await db.MoneyTransactions
            .Include(c => c.Category)
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.Id == id);
    }
    
    public async Task<List<MoneyTransaction>> GetAllTransactionsByUserIdAsync(long userId)
    {
        return await db.MoneyTransactions
            .Include(c => c.Category)
            .Include(c => c.Items)
            .Where(category => category.UserId == userId)
            .ToListAsync();
    }

    public async Task<MoneyTransaction> UpdateCategoryAsync(MoneyTransaction transaction)
    {
        db.MoneyTransactions.Update(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    public async Task<bool> DeleteCategoryAsync(long id)
    {
        var transaction = await GetByIdAsync(id);
        if (transaction == null) throw new BusinessException("Транзакция не найдена");

        foreach (var item in transaction.Items)
        {
            db.PurchaseItems.Remove(item);
        }

        db.MoneyTransactions.Remove(transaction);
        await db.SaveChangesAsync();
        return true;
    }
}
