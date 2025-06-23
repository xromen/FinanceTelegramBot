using Microsoft.EntityFrameworkCore;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Services;
public class KeywordService(ApplicationDbContext db, FamilyService familyService, CategoryAndKeywordNamesService nameService)
{
    public async Task<CategoryKeyword> CreateAsync(CategoryKeyword categoryKeyword)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryKeyword.CategoryId);

        var nameTaken = await nameService.CategoryNameExists(category.UserId, categoryKeyword.Keyword) ||
            await nameService.KeywordNameExists(category.UserId, categoryKeyword.Keyword);

        //Проверям наличие категории с таким же именем у пользователя
        //var nameTaken = await db.Categories
        //    .Include(c => c.Keywords)
        //    .Where(c => c.UserId == category.UserId)
        //    .AnyAsync(c => c.Name.ToLower() == categoryKeyword.Keyword.ToLower());

        ////Проверяем наличие ключевого слова с таким же именем у пользователя
        //nameTaken = nameTaken ||
        //    await db.CategoryKeywords
        //    .Include(c => c.Category)
        //    .Where(c => c.Category.UserId == category.UserId)
        //    .AnyAsync(c => c.Keyword.ToLower() == categoryKeyword.Keyword.ToLower());

        if (nameTaken)
        {
            throw new BusinessException("Данное ключевое слово уже занято.");
        }

        await db.CategoryKeywords.AddAsync(categoryKeyword);
        await db.SaveChangesAsync();
        return categoryKeyword;
    }

    //public async Task<bool> NameExists(long userId, string name)
    //{
    //    var keywords = await GetAllByUserIdAsync(userId);
    //    return keywords.Any(c => c.Keyword.ToLower() == name.ToLower());
    //}

    public async Task<CategoryKeyword?> GetByIdAsync(long id)
    {
        return await db.CategoryKeywords.Include(c => c.Category).SingleOrDefaultAsync(c => c.Id == id);
    }
    public async Task<List<CategoryKeyword>> GetAllKeywordsByCategoryIdAsync(long categortId)
    {
        return await db.CategoryKeywords.Include(c => c.Category).Where(c => c.CategoryId == categortId).ToListAsync();
    }

    public async Task<List<CategoryKeyword>> GetAllByUserIdAsync(long userId)
    {
        var family = await familyService.GetFamilyByMemberId(userId);
        var membersIds = family.Members.Select(c => c.Id);

        return await db.CategoryKeywords
            .Include(c => c.Category)
            .Where(c => c.Category.UserId == userId || membersIds.Contains(c.Category.UserId))
            .ToListAsync();
    }

    public async Task<CategoryKeyword> UpdateCategoryAsync(CategoryKeyword keyword)
    {
        db.CategoryKeywords.Update(keyword);
        await db.SaveChangesAsync();
        return keyword;
    }

    public async Task<bool> DeleteKeywordAsync(long id)
    {
        var keyword = await GetByIdAsync(id);
        if (keyword == null) return false;

        db.CategoryKeywords.Remove(keyword);
        await db.SaveChangesAsync();
        return true;
    }
}
