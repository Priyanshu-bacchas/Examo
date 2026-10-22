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