using Microsoft.EntityFrameworkCore;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;
using System.Linq.Expressions;

namespace FinanceTelegramBot.Services;

public class MoneyTransactionService(ApplicationDbContext db, FamilyService familyService)
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
    
    public async Task<List<MoneyTransaction>> GetAllTransactionsByUserIdAsync(long userId, Expression<Func<MoneyTransaction, bool>>? expression = null)
    {
        var family = await familyService.GetFamilyByMemberId(userId);

        IQueryable<MoneyTransaction> transactions = db.MoneyTransactions
            .Include(c => c.Category)
            .Include(c => c.Items);

        if(family != null)
        {
            var memberIds = family.Members.Select(c => c.Id);

            transactions = transactions.Where(c => c.UserId == userId || memberIds.Contains(c.UserId));
        }
        else
        {
            transactions = transactions.Where(c => c.UserId == userId);
        }

        if(expression != null)
        {
            transactions = transactions.Where(expression);
        }

        return await transactions.ToListAsync();
    }

    public async Task<List<DateOnly>> GetTransactionsDate(long userId)
    {
        var family = await familyService.GetFamilyByMemberId(userId);

        List<DateOnly> dates = new();

        if (family != null)
        {
            var memberIds = family.Members.Select(c => c.Id);

            return await db.MoneyTransactions.Where(c => c.UserId == userId || memberIds.Contains(c.UserId)).Select(c => c.Date).ToListAsync();
        }
        else
        {
            return await db.MoneyTransactions.Where(c => c.UserId == userId).Select(c => c.Date).ToListAsync();
        }
    }

    public async Task<decimal> GetMonthBalance(long userId, int year, int month)
    {
        var transactions = await GetAllTransactionsByUserIdAsync(userId, c => c.Date.Year == year && c.Date.Month == month);
        var amounts = transactions.Select(c => { return c.Category.Type == TransactionType.Income ? c.Amount : -c.Amount; });
        
        return amounts.Sum();
    }

    public async Task<MoneyTransaction> UpdateAsync(MoneyTransaction transaction)
    {
        db.MoneyTransactions.Update(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    public async Task<bool> DeleteTransactionAsyns(long id)
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

    public async Task<bool> AnyAsync(Expression<Func<MoneyTransaction, bool>>? expression)
    {
        return await db.MoneyTransactions.AnyAsync(expression);
    }
}
