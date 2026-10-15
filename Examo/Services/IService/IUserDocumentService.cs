using Examo.Models;
using Microsoft.AspNetCore.Http;

namespace Examo.Services.Interfaces;

public interface IUserDocumentService
{
    Task<IEnumerable<UserDocument>> GetAllAsync();

    Task<UserDocument> UploadAsync(string? name, IFormFile file);

    // File kholne ka temporary link (sirf owner ke liye)
    Task<string?> GetUrlAsync(int id);

    // Download ke liye temporary link (file download hoti hai, khulti nahi)
    Task<string?> GetDownloadUrlAsync(int id);

    // Edit: document ka naam badalna
    Task<UserDocument?> RenameAsync(int id, string name);

    Task<bool> DeleteAsync(int id);
}