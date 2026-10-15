namespace Examo.Services;

// appsettings / environment variables: Supabase:Url, Supabase:SecretKey, Supabase:Bucket
public class SupabaseOptions
{
    public string Url { get; set; } = string.Empty;

    // Server-side secret key (sb_secret_...). Kabhi frontend me mat daalna.
    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "Examodoc";
}
