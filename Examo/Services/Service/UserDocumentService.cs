using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Examo.Services;

public class UserDocumentService : IUserDocumentService
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    // Sirf photo (jpg, png, webp) aur PDF
    private static readonly Dictionary<string, string> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private readonly IUserDocumentRepository _repository;
    private readonly ISupabaseStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public UserDocumentService(
        IUserDocumentRepository repository,
        ISupabaseStorageService storage,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<UserDocument>> GetAllAsync()
    {
        var documents = await _repository.GetAllAsync();

        return documents.OrderByDescending(x => x.CreatedAt);
    }

    public async Task<UserDocument> UploadAsync(
        string? name,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException(
                "Please select a file.");
        }

        if (file.Length > MaxBytes)
        {
            throw new ArgumentException(
                "File is too large. Maximum size is 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrEmpty(extension) ||
            !Allowed.TryGetValue(extension, out var contentType))
        {
            throw new ArgumentException(
                "Only photos (JPG, PNG, WEBP) and PDF files are allowed.");
        }

        var originalName = Path.GetFileName(file.FileName);

        var displayName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(originalName)
            : name.Trim();

        if (displayName.Length > 150)
            displayName = displayName[..150];

        if (originalName.Length > 255)
            originalName = originalName[^255..];

        var path =
            $"users/{_currentUser.UserId}/documents/" +
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

        await using (var stream = file.OpenReadStream())
        {
            await _storage.UploadAsync(path, stream, contentType);
        }

        var document = new UserDocument
        {
            Name = displayName,
            FileName = originalName,
            StoragePath = path,
            ContentType = contentType,
            SizeBytes = file.Length,
            Kind = contentType.StartsWith("image/")
                ? "Photo"
                : "PDF"
        };

        try
        {
            return await _repository.AddAsync(document);
        }
        catch
        {
            // DB save fail hua to storage me file akeli na rahe
            await _storage.DeleteAsync(path);
            throw;
        }
    }

    public async Task<string?> GetUrlAsync(int id)
    {
        var document = await _repository.GetByIdAsync(id);

        if (document == null)
            return null;

        return await _storage.CreateSignedUrlAsync(
            document.StoragePath);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var document = await _repository.GetByIdAsync(id);

        if (document == null)
            return false;

        var path = document.StoragePath;

        var deleted = await _repository.DeleteAsync(id);

        if (deleted)
        {
            await _storage.DeleteAsync(path);
        }

        return deleted;
    }
}
