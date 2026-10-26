using Examo.Models;

namespace Examo.Services;

// Exam date ke hisaab se Exam ka status:
//   date nikal chuki (kal ya usse pehle) -> Done
//   aaj ya aage ki date                  -> Coming Soon
public static class ExamStatusHelper
{
    public const string Done = "Done";
    public const string ComingSoon = "Coming Soon";

    // Aaj ki date India (IST) ke hisaab se
    public static DateOnly Today()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
    }

    public static string FromDate(DateOnly? examDate)
    {
        return examDate.HasValue && examDate.Value < Today()
            ? Done
            : ComingSoon;
    }
}
