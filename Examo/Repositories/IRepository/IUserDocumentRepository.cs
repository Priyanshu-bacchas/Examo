using Examo.Models;

namespace Examo.Repositories.Interfaces;

public interface IUserDocumentRepository
{
    Task<IEnumerable<UserDocument>> GetAllAsync();
    Task<UserDocument?> GetByIdAsync(int id);
    Task<UserDocument> AddAsync(UserDocument document);
    Task<UserDocument> UpdateAsync(UserDocument document);
    Task<bool> DeleteAsync(int id);
}
