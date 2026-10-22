using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Support;

public class FaqDto
{
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;
}

public class FeaturedGroupDto
{
    public string Title { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public List<string> Links { get; set; } = new();
}

public class HelpContentDto
{
    public List<FeaturedGroupDto> Featured { get; set; } = new();

    public List<FaqDto> Faqs { get; set; } = new();
}

public class FaqSearchResultDto
{
    public string Query { get; set; } = string.Empty;

    public bool Found { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<FaqDto> Results { get; set; } = new();
}

// Help > Contact Us form
// Name aur Email yahan nahi hain: backend login user (JWT) ke account se khud leta hai,
// isliye client kuch bhi bheje use ignore kiya jaata hai.
public class ContactSupportDto
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = "Subject can be at most 150 characters.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message is required.")]
    [StringLength(3000, MinimumLength = 10,
        ErrorMessage = "Message must be between 10 and 3000 characters.")]
    public string Message { get; set; } = string.Empty;
}

public class ContactSupportResponseDto
{
    public string Message { get; set; } = string.Empty;
}

// ---------- Admin: Help & Support ----------

public class SupportMessageDto
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? AdminReply { get; set; }

    public DateTime? RepliedAt { get; set; }

    public string? RepliedBy { get; set; }
}

public class SupportMessageStatsDto
{
    public int Total { get; set; }

    public int Open { get; set; }

    public int Resolved { get; set; }

    public int Last7Days { get; set; }
}

public class SupportMessageListDto
{
    public SupportMessageStatsDto Stats { get; set; } = new();

    public List<SupportMessageDto> Items { get; set; } = new();
}

public class UpdateSupportStatusDto
{
    public string? Status { get; set; }
}

// Admin ka reply (email se student ko jaata hai)
public class ReplySupportDto
{
    [Required(ErrorMessage = "Reply is required.")]
    [StringLength(5000, MinimumLength = 2,
        ErrorMessage = "Reply must be between 2 and 5000 characters.")]
    public string Reply { get; set; } = string.Empty;

    // Reply ke baad message ko Resolved mark karna hai ya nahi
    public bool MarkResolved { get; set; } = true;
}