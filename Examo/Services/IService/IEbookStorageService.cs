namespace Examo.Services.Interfaces;

public interface IEbookStorageService
{
    Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<string> CreateSignedUrlAsync(string path, int expiresInSeconds = 600, string? downloadFileName = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
