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

        // ContentType extension se tay hota hai (client ka ContentType
        // kabhi galat aata hai), asli check neeche signature se.
        if (string.IsNullOrEmpty(extension) ||
            !Allowed.TryGetValue(extension, out var contentType))
        {
            throw new ArgumentException(
                "Only photos (JPG, PNG, WEBP) and PDF files are allowed.");
        }

        if (!await MatchesSignatureAsync(file, contentType))
        {
            throw new ArgumentException(
                "File content does not match its type. " +
                "Please select a valid photo or PDF.");
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

    // File ke shuruaati bytes check karta hai (PDF / JPG / PNG / WEBP)
    private static async Task<bool> MatchesSignatureAsync(
        IFormFile file,
        string contentType)
    {
        var header = new byte[12];

        await using var stream = file.OpenReadStream();

        var read = 0;

        while (read < header.Length)
        {
            var n = await stream.ReadAsync(
                header.AsMemory(read, header.Length - read));

            if (n == 0)
                break;

            read += n;
        }

        switch (contentType)
        {
            case "application/pdf":
                return read >= 5 &&
                       header[0] == 0x25 && header[1] == 0x50 &&
                       header[2] == 0x44 && header[3] == 0x46 &&
                       header[4] == 0x2D;

            case "image/jpeg":
                return read >= 3 &&
                       header[0] == 0xFF && header[1] == 0xD8 &&
                       header[2] == 0xFF;

            case "image/png":
                return read >= 8 &&
                       header[0] == 0x89 && header[1] == 0x50 &&
                       header[2] == 0x4E && header[3] == 0x47 &&
                       header[4] == 0x0D && header[5] == 0x0A &&
                       header[6] == 0x1A && header[7] == 0x0A;

            case "image/webp":
                return read >= 12 &&
                       header[0] == 0x52 && header[1] == 0x49 &&
                       header[2] == 0x46 && header[3] == 0x46 &&
                       header[8] == 0x57 && header[9] == 0x45 &&
                       header[10] == 0x42 && header[11] == 0x50;

            default:
                return false;
        }
    }
}