using Examo.Services.Interfaces;

namespace Examo.Services;

public class EbookStorageService : IEbookStorageService
{
    private readonly ISupabaseStorageService _storage;

    public EbookStorageService(ISupabaseStorageService storage)
    {
        _storage = storage;
    }

    public Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default) =>
        _storage.UploadAsync(path, content, contentType, cancellationToken);

    public Task<string> CreateSignedUrlAsync(string path, int expiresInSeconds = 600, string? downloadFileName = null, CancellationToken cancellationToken = default) =>
        _storage.CreateSignedUrlAsync(path, expiresInSeconds, downloadFileName, cancellationToken);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        _storage.DeleteAsync(path, cancellationToken);
}
