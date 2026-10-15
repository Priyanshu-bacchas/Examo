namespace Examo.Services.Interfaces;

public interface ISupabaseStorageService
{
    Task UploadAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    // Private bucket ki file ko thodi der ke liye kholne wala link.
    // downloadFileName dene par link browser me file ko download karwata hai.
    Task<string> CreateSignedUrlAsync(
        string path,
        int expiresInSeconds = 600,
        string? downloadFileName = null,
        CancellationToken cancellationToken = default);

    // Best effort: fail ho to exception nahi phenkta
    Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default);
}