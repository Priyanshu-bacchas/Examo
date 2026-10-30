namespace Examo.Models;

// Admin se badalne wali chhoti settings (jaise free trial ke din).
// Row na ho to code ki default value chalti hai.
public class AppSetting
{
    public int Id { get; set; }

    // jaise "trial-days"
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}
