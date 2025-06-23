using Microsoft.EntityFrameworkCore;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Services;

public class UserService(ApplicationDbContext db)
{
    public async Task<User> CreateAsync(User user)
    {
        if (await UserExistsAsync(user.Id))
        {
            throw new BusinessException("Пользователь уже добавлен");
        }

        if(user.CreatedAt == default)
        {
            user.CreatedAt = DateTime.Now;
        }

        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        return user;
    }

    public async Task<User?> GetUserByIdAsync(long userId)
    {
        return await db.Users.Include(c => c.Family).ThenInclude(c => c.Members).SingleOrDefaultAsync(c => c.Id == userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await db.Users.Include(c => c.Family).SingleOrDefaultAsync(c => c.Username == username);
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync();

        return user;
    }

    public async Task<bool> DeleteUserAsync(long userId)
    {
        var user = await GetUserByIdAsync(userId);

        if(user == null)
        {
            throw new BusinessException("Пользователь не найден");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UserExistsAsync(long userId)
    {
        return await db.Users.AnyAsync(c => c.Id == userId);
    }
}
