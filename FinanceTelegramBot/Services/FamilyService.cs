using Microsoft.EntityFrameworkCore;
using System.Linq;
using FinanceTelegramBot.Data;
using FinanceTelegramBot.Data.Entities;
using FinanceTelegramBot.Models;

namespace FinanceTelegramBot.Services;

public class FamilyService(ApplicationDbContext db, UserService userService)
{
    public async Task<Family> CreateAsync(Family family)
    {
        if (await HasFamilyAsync(family.OwnerId))
        {
            throw new BusinessException("Вначале покиньте текущую семью");
        }

        var user = await userService.GetUserByIdAsync(family.OwnerId);

        if (user == null)
        {
            throw new BusinessException("Пользователь не найден");
        }

        await db.AddAsync(family);
        await db.SaveChangesAsync();

        user.FamilyId = family.Id;

        await userService.UpdateUserAsync(user);

        return family;
    }

    public async Task<User> AddFamilyMemberByIdAsync(long familyId, long memberId)
    {
        if (await HasFamilyAsync(memberId))
        {
            throw new BusinessException("Пользователь уже состоит в семье");
        }

        var user = await userService.GetUserByIdAsync(memberId);

        if (user == null)
        {
            throw new BusinessException("Пользователь не найден");
        }

        user.FamilyId = familyId;

        await userService.UpdateUserAsync(user);

        return user;
    }

    public async Task<User> AddFamilyMemberByUsernameAsync(long familyId, string username)
    {
        if (username[0] == '@')
        {
            username = username.Substring(1);
        }

        if (await HasFamilyAsync(username))
        {
            throw new BusinessException("Пользователь уже состоит в семье");
        }

        var user = await userService.GetUserByUsernameAsync(username);

        if (user == null)
        {
            throw new BusinessException("Пользователь не найден");
        }

        user.FamilyId = familyId;

        await userService.UpdateUserAsync(user);

        return user;
    }

    public async Task<Family?> GetFamilyByOwnerId(long ownerId)
    {
        return await db.Families
            .Include(c => c.Members)
            .Include(c => c.Owner)
            .SingleOrDefaultAsync(c => c.OwnerId == ownerId);
    }

    public async Task<Family?> GetFamilyByMemberId(long memberId)
    {
        var user = await userService.GetUserByIdAsync(memberId);

        if (user == null)
        {
            throw new BusinessException("Пользователь не найден");
        }

        //if (user.FamilyId == null)
        //{
        //    throw new BusinessException("У пользователя нет семьи");
        //}

        return user.Family;
    }

    public async Task<Family?> GetFamilyById(long ownerId)
    {
        return await db.Families
            .Include(c => c.Members)
            .Include(c => c.Owner)
            .SingleOrDefaultAsync(c => c.Id == ownerId);
    }

    public async Task<Family> UpdateAsync(Family family)
    {
        db.Families.Update(family);
        await db.SaveChangesAsync();

        return family;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var family = await GetFamilyById(id);

        if (family == null)
        {
            throw new BusinessException("Семья не найдена");
        }

        foreach (var member in family.Members)
        {
            member.FamilyId = null;
        }

        db.Families.Remove(family);

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> HasFamilyAsync(long userId)
    {
        return await db.Users.AnyAsync(c => c.Id == userId && c.FamilyId != null);
    }

    public async Task<bool> HasFamilyAsync(string username)
    {
        return await db.Users.AnyAsync(c => c.Username == username && c.FamilyId != null);
    }
}
