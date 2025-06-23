using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Services;
public class CategoryService(ApplicationDbContext db, FamilyService familyService, CategoryAndKeywordNamesService nameService)
{
    public async Task<Category> CreateAsync(Category category)
    {
        //Проверям наличие категории с таким же именем у пользователя
        var nameTaken = await nameService.CategoryNameExists(category.UserId, category.Name) || 
            await nameService.KeywordNameExists(category.UserId, category.Name);

        //Проверяем наличие ключевого слова с таким же именем у пользователя
        //nameTaken = nameTaken ||
        //    await db.CategoryKeywords
        //    .Include(c => c.Category)
        //    .Where(c => c.Category.UserId == category.UserId)
        //    .AnyAsync(c => c.Keyword.ToLower() == category.Name.ToLower());

        if (nameTaken)
        {
            throw new BusinessException("Данное имя категории уже занято.");
        }

        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();
        return category;
    }

    //public async Task<bool> NameExists(long userId, string name)
    //{
    //    var categories = await GetAllCategoriesByUserIdAsync(userId);
    //    return categories.Any(c => c.Name.ToLower() == name.ToLower());
    //}

    public async Task<Category?> GetByIdAsync(long id)
    {
        return await db.Categories
            .Include(c => c.Keywords)
            .SingleOrDefaultAsync(c => c.Id == id);
    }
    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        return await db.Categories
            .Include(c => c.Keywords)
            .ToListAsync();
    }
    public async Task<List<Category>> GetAllCategoriesByUserIdAsync(long userId, TransactionType? type = null)
    {
        var family = await familyService.GetFamilyByMemberId(userId);
        var membersIds = family.Members.Select(c => c.Id);

        return await db.Categories
            .Include(c => c.Keywords)
            .Where(c => (c.UserId == userId || membersIds.Contains(c.UserId)) && (type == null || c.Type == type) )
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByKeyword(long userId, string keyword)
    {
        return (await GetAllCategoriesByUserIdAsync(userId))
            .FirstOrDefault(c => c.Keywords.Any(k => k.Keyword.ToLower() == keyword.ToLower()) || c.Name.ToLower() == keyword.ToLower());
    }

    public async Task<bool> AnyAsync(Expression<Func<Category, bool>> expression)
    {
        return await db.Categories.AnyAsync(expression);
    }

    public async Task<Category> UpdateCategoryAsync(Category category)
    {
        db.Categories.Update(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<bool> DeleteCategoryAsync(long id)
    {
        var category = await GetByIdAsync(id);
        if (category == null) throw new BusinessException("Категория не найдена");

        foreach (var keyword in category.Keywords)
        {
            db.CategoryKeywords.Remove(keyword);
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return true;
    }
}
