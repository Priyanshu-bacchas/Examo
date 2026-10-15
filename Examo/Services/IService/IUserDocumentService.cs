using Examo.Models;
using Microsoft.AspNetCore.Http;

namespace Examo.Services.Interfaces;

public interface IUserDocumentService
{
    Task<IEnumerable<UserDocument>> GetAllAsync();

    Task<UserDocument> UploadAsync(string? name, IFormFile file);

    // File kholne ka temporary link (sirf owner ke liye)
    Task<string?> GetUrlAsync(int id);

    Task<bool> DeleteAsync(int id);
}
