using Examo.Models;

using Microsoft.EntityFrameworkCore;

namespace Examo.Services;

// Admin sirf "Latest Job" me entry banata hai. Us job ke naam se Exam,
// Result aur Admit Card sections me entries apne aap ban jaati hain
// (Draft me). Admin un sections me sirf Edit / Visibility / Delete karta hai.
public static class JobSectionSync
{
    private static readonly string[] ChildCategories =
    {
        JobPost.CategoryExam,
        JobPost.CategoryResult,
        JobPost.CategoryAdmitCard
    };

    // Job ke liye teeno sections me entry banao.
    // Same naam ki entry pehle se ho (admin ne khud banayi thi) to wahi job se
    // jud jaati hai, naya duplicate nahi banta.
    public static async Task EnsureChildrenAsync(
        ExamoDbContext context,
        JobPost job)
    {
        var key = job.ExamName.Trim().ToLower();

        foreach (var category in ChildCategories)
        {
            var existing = await context.JobPosts.FirstOrDefaultAsync(x =>
                x.Category == category &&
                (x.SourceJobId == job.Id ||
                 (x.SourceJobId == null &&
                  x.ExamName.ToLower() == key)));

            if (existing != null)
            {
                if (existing.SourceJobId == null)
                    existing.SourceJobId = job.Id;

                continue;
            }

            context.JobPosts.Add(new JobPost
            {
                Category = category,
                ExamName = job.ExamName,

                // Exam section me link nahi hota
                Link = category == JobPost.CategoryExam
                    ? string.Empty
                    : job.Link,

                // Result / Admit Card: date admin baad me Edit se bharta hai
                Status = category == JobPost.CategoryExam
                    ? null
                    : "Upcoming",

                // Admin Details / date bharke Publish karega
                IsDraft = true,

                SourceJobId = job.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
