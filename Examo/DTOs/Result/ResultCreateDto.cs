using System.ComponentModel.DataAnnotations;

namespace Examo.DTOs.Result;

public class ResultCreateDto
{
    [Required]
    [MaxLength(200)]
    public string ExamName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Awaited|Qualified|Not Qualified)$",
        ErrorMessage =
            "Result must be Awaited, Qualified, or Not Qualified.")]
    public string Result { get; set; } = "Awaited";
}
