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
public class ContactSupportDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, ErrorMessage = "Name can be at most 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(200, ErrorMessage = "Email can be at most 200 characters.")]
    public string Email { get; set; } = string.Empty;

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