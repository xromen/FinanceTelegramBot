namespace FinanceTelegramBot.Services;

public class CategoryAndKeywordNamesService(Lazy<CategoryService> categoryService, Lazy<KeywordService> keywordService)
{
    public async Task<bool> CategoryNameExists(long userId, string name)
    {
        var categories = await categoryService.Value.GetAllCategoriesByUserIdAsync(userId);
        return categories.Any(c => c.Name.ToLower() == name.ToLower());
    }

    public async Task<bool> KeywordNameExists(long userId, string name)
    {
        var keywords = await keywordService.Value.GetAllByUserIdAsync(userId);
        return keywords.Any(c => c.Keyword.ToLower() == name.ToLower());
    }
}
