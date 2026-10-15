using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Examo.Repositories;

public class UserDocumentRepository : IUserDocumentRepository
{
    private readonly ExamoDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UserDocumentRepository(
        ExamoDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    // Sirf logged-in user ke records
    public async Task<IEnumerable<UserDocument>> GetAllAsync()
    {
        var userId = _currentUser.UserId;

        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserDocument?> GetByIdAsync(int id)
    {
        var userId = _currentUser.UserId;

        return await _context.Documents
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);
    }

    public async Task<UserDocument> AddAsync(UserDocument document)
    {
        // Record hamesha current user ke naam par banta hai
        document.UserId = _currentUser.UserId;

        await _context.Documents.AddAsync(document);
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<UserDocument> UpdateAsync(UserDocument document)
    {
        if (document.UserId != _currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "You can only modify your own records.");
        }

        _context.Documents.Update(document);
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var userId = _currentUser.UserId;

        var document = await _context.Documents
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.UserId == userId);

        if (document == null)
        {
            return false;
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return true;
    }
}
