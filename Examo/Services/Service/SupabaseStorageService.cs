using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Examo.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Examo.Services;

// Supabase Storage REST API (private bucket) - upload, signed URL, delete
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        HttpClient http,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseStorageService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private string BaseUrl =>
        _options.Url.TrimEnd('/') + "/storage/v1";

    private string BucketPart =>
        Uri.EscapeDataString(_options.Bucket);

    private void EnsureConfigured()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Url))
            missing.Add("Url");

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            missing.Add("SecretKey");

        if (string.IsNullOrWhiteSpace(_options.Bucket))
            missing.Add("Bucket");

        if (missing.Count > 0)
        {
            var environment =
                Environment.GetEnvironmentVariable(
                    "ASPNETCORE_ENVIRONMENT") ?? "Production";

            // Sirf naam batate hain, value kabhi nahi
            throw new InvalidOperationException(
                "File storage is not configured. Missing Supabase: " +
                string.Join(", ", missing) +
                $" (server environment: {environment}).");
        }

        var publicKind = GetPublicKeyKind(_options.SecretKey);

        if (publicKind != null)
        {
            var key = _options.SecretKey.Trim();

            var environment =
                Environment.GetEnvironmentVariable(
                    "ASPNETCORE_ENVIRONMENT") ?? "Production";

            // Sirf key ka type aur aakhri 4 akshar (public key hai, secret nahi)
            throw new InvalidOperationException(
                $"Supabase key is the PUBLIC {publicKind} key " +
                $"(ends with ...{key[^Math.Min(4, key.Length)..]}, " +
                $"server environment: {environment}). " +
                "Use the SECRET key (sb_secret_...) from " +
                "Supabase > Settings > API Keys.");
        }
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url)
    {
        var request = new HttpRequestMessage(method, url);

        var key = _options.SecretKey.Trim();

        request.Headers.TryAddWithoutValidation("apikey", key);

        // Naye sb_secret_ keys JWT nahi hain: sirf apikey header me jaate hain.
        // Purani (legacy) service_role JWT key Bearer me bhi jaati hai.
        if (!key.StartsWith("sb_", StringComparison.Ordinal))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", key);
        }

        return request;
    }

    // publishable (sb_publishable_...) ya legacy anon JWT = public key, storage ke liye galat
    private static string? GetPublicKeyKind(string key)
    {
        key = key.Trim();

        if (key.StartsWith("sb_publishable_", StringComparison.Ordinal))
            return "publishable";

        var parts = key.Split('.');

        if (parts.Length != 3)
            return null;

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            payload = payload.PadRight(
                payload.Length + (4 - payload.Length % 4) % 4,
                '=');

            using var json = JsonDocument.Parse(
                Convert.FromBase64String(payload));

            return json.RootElement.TryGetProperty(
                       "role",
                       out var role) &&
                   role.GetString() == "anon"
                ? "anon (legacy)"
                : null;
        }
        catch
        {
            return null;
        }
    }

    // Supabase ke error body se chhota reason (secret nahi hota)
    private static string ReadReason(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);

            foreach (var name in new[] { "message", "error" })
            {
                if (json.RootElement.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // JSON nahi hai
        }

        return body.Length > 150 ? body[..150] : body;
    }

    private static string EncodePath(string path) =>
        string.Join(
            "/",
            path.Split('/')
                .Select(Uri.EscapeDataString));

    public async Task UploadAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"{BaseUrl}/object/{BucketPart}/{EncodePath(path)}");

        request.Headers.TryAddWithoutValidation(
            "x-upsert",
            "false");

        request.Content = new StreamContent(content);

        request.Content.Headers.ContentType =
            MediaTypeHeaderValue.Parse(contentType);

        using var response =
            await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            _logger.LogError(
                "Supabase upload failed ({Status}): {Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"File upload failed ({(int)response.StatusCode}): " +
                ReadReason(body));
        }
    }

    public async Task<string> CreateSignedUrlAsync(
        string path,
        int expiresInSeconds = 600,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"{BaseUrl}/object/sign/{BucketPart}/{EncodePath(path)}");

        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new { expiresIn = expiresInSeconds }),
            Encoding.UTF8,
            "application/json");

        using var response =
            await _http.SendAsync(request, cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Supabase sign failed ({Status}): {Body}",
                (int)response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"File could not be opened ({(int)response.StatusCode}): " +
                ReadReason(body));
        }

        using var json = JsonDocument.Parse(body);

        var signed =
            json.RootElement
                .GetProperty("signedURL")
                .GetString() ?? string.Empty;

        if (signed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return signed;

        var root = _options.Url.TrimEnd('/');

        if (signed.StartsWith("/storage/v1", StringComparison.Ordinal))
            return root + signed;

        return BaseUrl +
               (signed.StartsWith("/") ? signed : "/" + signed);
    }

    public async Task DeleteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureConfigured();

            using var request = CreateRequest(
                HttpMethod.Delete,
                $"{BaseUrl}/object/{BucketPart}/{EncodePath(path)}");

            using var response =
                await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode &&
                (int)response.StatusCode != 404)
            {
                _logger.LogWarning(
                    "Supabase delete failed ({Status}) for {Path}",
                    (int)response.StatusCode,
                    path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Supabase delete failed for {Path}",
                path);
        }
    }
}
