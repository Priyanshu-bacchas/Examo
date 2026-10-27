using Examo.DTOs.Jobs;
using Examo.Filters;
using Examo.Models;
using Examo.Services;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

// Jobs / Results / Admit Cards
//   GET     /api/Jobs?category=Job|Result|AdmitCard|Exam   -> koi bhi logged-in user
//   POST    /api/Jobs                                 -> sirf Admin
//   PUT     /api/Jobs/{id}                            -> sirf Admin
//   DELETE  /api/Jobs/{id}                            -> sirf Admin
//   PUT     /api/Jobs/{id}/publish                    -> sirf Admin (Draft <-> Published)
//   POST    /api/Jobs/{id}/add                        -> logged-in user (Job ko apni list me add karo)
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private static readonly string[] Categories =
    {
        JobPost.CategoryJob,
        JobPost.CategoryResult,
        JobPost.CategoryAdmitCard,
        JobPost.CategoryExam
    };

    private static readonly string[] Statuses = { "Out", "Upcoming" };

    private readonly ExamoDbContext _context;
    private readonly IConfiguration _config;
    private readonly ICurrentUserService _currentUser;

    public JobsController(
        ExamoDbContext context,
        IConfiguration config,
        ICurrentUserService currentUser)
    {
        _context = context;
        _config = config;
        _currentUser = currentUser;
    }

    // =====================================================
    // LIST
    //   activeOnly=true -> sirf wo Jobs jinki last date nikli nahi (students ke liye)
    // =====================================================
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool includeDrafts = false,
        [FromQuery] int take = 200)
    {
        var cat = NormalizeCategory(category);

        if (cat == null)
        {
            return BadRequest(new
            {
                message = "Category must be Job, Result, AdmitCard or Exam."
            });
        }

        var query = _context.JobPosts
            .AsNoTracking()
            .Where(x => x.Category == cat);

        // Draft entries sirf Admin ko (includeDrafts=true par) dikhti hain.
        // Students ko hamesha sirf Published entries milti hain.
        if (!(includeDrafts && _currentUser.IsAdmin))
            query = query.Where(x => !x.IsDraft);

        if (activeOnly && cat == JobPost.CategoryJob)
        {
            var offset = _config.GetValue(
                "Notifications:UtcOffsetMinutes",
                330);

            var today = DateOnly.FromDateTime(
                DateTime.UtcNow.AddMinutes(offset));

            query = query.Where(x =>
                x.LastDate == null || x.LastDate >= today);
        }

        // Jobs: jo naya add hua wo upar. Result / Admit Card: naye date wale upar.
        var ordered = cat == JobPost.CategoryJob || cat == JobPost.CategoryExam
            ? query.OrderByDescending(x => x.Id)
            : query
                .OrderByDescending(x => x.PostDate)
                .ThenByDescending(x => x.Id);

        var rows = await ordered
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => new
            {
                x.Id,
                x.Category,
                x.ExamName,
                x.StartDate,
                x.LastDate,
                x.ExamDate,
                x.Syllabus,
                x.Details,
                x.IsDraft,
                x.PostDate,
                x.Status,
                x.Link,
                x.CreatedAt
            })
            .ToListAsync();

        // Jobs me har job ke saath IsAdded bhejte hain: user ne ye job
        // pehle se apni Exam Forms list me add ki hai ya nahi.
        // Job list me hamesha dikhti rehti hai, bas button disable hota hai.
        var addedIds = new HashSet<int>();

        if (cat == JobPost.CategoryJob && rows.Count > 0)
        {
            var userId = _currentUser.UserIdOrNull;

            if (userId != null)
            {
                var jobIds = rows.Select(x => x.Id).ToList();
                var jobNames = rows
                    .Select(x => x.ExamName.Trim().ToLower())
                    .Distinct()
                    .ToList();

                var forms = await _context.ExamForms
                    .AsNoTracking()
                    .Where(f =>
                        f.UserId == userId &&
                        ((f.SourceJobId != null &&
                          jobIds.Contains(f.SourceJobId.Value)) ||
                         (f.SourceJobId == null &&
                          jobNames.Contains(f.ExamName.ToLower()))))
                    .Select(f => new { f.SourceJobId, f.ExamName })
                    .ToListAsync();

                foreach (var f in forms)
                {
                    if (f.SourceJobId != null)
                    {
                        addedIds.Add(f.SourceJobId.Value);
                    }
                    else
                    {
                        // Purana record (link hone se pehle ka) - naam se match
                        var key = f.ExamName.Trim().ToLower();

                        foreach (var r in rows.Where(r =>
                            r.ExamName.Trim().ToLower() == key))
                        {
                            addedIds.Add(r.Id);
                        }
                    }
                }
            }
        }

        var items = rows.Select(x => new
        {
            x.Id,
            x.Category,
            x.ExamName,
            x.StartDate,
            x.LastDate,
            x.ExamDate,
            x.Syllabus,
            x.Details,
            x.PostDate,
            PublishStatus = x.IsDraft ? "Draft" : "Published",
            x.Status,
            x.Link,
            x.CreatedAt,
            IsAdded = addedIds.Contains(x.Id)
        });

        return Ok(items);
    }

    // =====================================================
    // CREATE (Admin)
    // =====================================================
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] JobPostDto dto)
    {
        var cat = NormalizeCategory(dto.Category);
        var error = Validate(cat, dto);

        if (error != null)
            return BadRequest(new { message = error });

        var item = new JobPost
        {
            Category = cat!,
            CreatedAt = DateTime.UtcNow
        };

        Apply(item, dto);

        _context.JobPosts.Add(item);
        await _context.SaveChangesAsync();

        // Latest Job banate hi Exam / Result / Admit Card me entries (Draft) ban jaati hain
        if (item.Category == JobPost.CategoryJob)
            await JobSectionSync.EnsureChildrenAsync(_context, item);

        HttpContext.Items[AuditLogFilter.ActionKey] =
            $"Add {Label(cat!)}";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            item.ExamName;

        return Ok(new { item.Id });
    }

    // =====================================================
    // ADD TO MY LIST (logged-in user)
    //   Job ki details user ke Exam Forms, Exams, Preparation aur
    //   Results me add hoti hain. Default status:
    //     Exam Form -> Pending, Exam -> Coming Soon,
    //     Preparation -> Not Started, Result -> Awaited
    //   User baad me status khud change kar sakta hai.
    //   Jo record pehle se (same exam name ka) user ke paas hai,
    //   wo dobara nahi banta.
    // =====================================================
    [HttpPost("{id:int}/add")]
    public async Task<IActionResult> AddToMyList(int id)
    {
        var userId = _currentUser.UserId;

        var job = await _context.JobPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Category == JobPost.CategoryJob &&
                !x.IsDraft);

        if (job == null)
            return NotFound(new { message = "Job not found." });

        // Last date nikal gayi ho to job add nahi hogi
        var utcOffset = _config.GetValue(
            "Notifications:UtcOffsetMinutes",
            330);

        var todayDate = DateOnly.FromDateTime(
            DateTime.UtcNow.AddMinutes(utcOffset));

        if (job.LastDate != null && job.LastDate < todayDate)
        {
            return BadRequest(new
            {
                message = "The last date for this job has passed."
            });
        }

        var name = job.ExamName.Trim();
        var key = name.ToLower();

        var added = new List<string>();
        var updated = new List<string>();
        var skipped = new List<string>();

        // Is job se bana record ya (purana) same naam wala record
        // ko pehchano aur job se jod do (SourceJobId).

        // Start date last date ke baad ho to start ko last date par rakho
        var formEnd = job.LastDate
            ?? job.StartDate
            ?? todayDate;
        var formStart = job.StartDate ?? todayDate;

        if (formStart > formEnd)
            formStart = formEnd;

        // ---- Exam Form ----
        var form = await _context.ExamForms.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            (x.SourceJobId == job.Id ||
             (x.SourceJobId == null && x.ExamName.ToLower() == key)));

        if (form == null)
        {
            _context.ExamForms.Add(new ExamForm
            {
                ExamName = name,
                RegisterStartDate = formStart,
                RegisterEndDate = formEnd,
                Link = job.Link,
                Status = "Pending",
                SourceJobId = job.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            added.Add("Exam Forms");
        }
        else
        {
            form.SourceJobId = job.Id;

            skipped.Add("Exam Forms");
        }

        // ---- Exam ----
        // Pehle se hai (e.g. Exam Form se auto bana) to job se jod do
        // aur date job ki date se match kar do
        var exam = await _context.Exams.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            (x.SourceJobId == job.Id ||
             (x.SourceJobId == null && x.ExamName.ToLower() == key)));

        if (exam == null)
        {
            _context.Exams.Add(new Exam
            {
                ExamName = name,
                ExamDate = job.ExamDate,
                Status = ExamStatusHelper.FromDate(job.ExamDate),
                SourceJobId = job.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            added.Add("Exams");
        }
        else
        {
            exam.SourceJobId = job.Id;

            if (job.ExamDate != null && exam.ExamDate != job.ExamDate)
            {
                exam.ExamDate = job.ExamDate;
                exam.Status = ExamStatusHelper.FromDate(job.ExamDate);

                updated.Add("Exams");
            }
            else
            {
                skipped.Add("Exams");
            }
        }

        // ---- Preparation ----
        var prep = await _context.Preparations.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            (x.SourceJobId == job.Id ||
             (x.SourceJobId == null && x.ExamName.ToLower() == key)));

        if (prep == null)
        {
            _context.Preparations.Add(new Preparation
            {
                ExamName = name,
                Syllabus = job.Syllabus,
                Status = "Not Started",
                SourceJobId = job.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            added.Add("Preparation");
        }
        else
        {
            prep.SourceJobId = job.Id;

            if (string.IsNullOrWhiteSpace(prep.Syllabus) &&
                !string.IsNullOrWhiteSpace(job.Syllabus))
            {
                prep.Syllabus = job.Syllabus;

                updated.Add("Preparation");
            }
            else
            {
                skipped.Add("Preparation");
            }
        }

        // ---- Result ----
        var result = await _context.Results.FirstOrDefaultAsync(x =>
            x.UserId == userId &&
            (x.SourceJobId == job.Id ||
             (x.SourceJobId == null && x.ExamName.ToLower() == key)));

        if (result == null)
        {
            _context.Results.Add(new ExamResult
            {
                ExamName = name,
                Result = "Awaited",
                SourceJobId = job.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            added.Add("Results");
        }
        else
        {
            result.SourceJobId = job.Id;

            skipped.Add("Results");
        }

        // Links (SourceJobId) bhi save hone chahiye
        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] = "Add Job To My List";
        HttpContext.Items[AuditLogFilter.DetailsKey] = name;

        string message;

        if (added.Count == 0 && updated.Count == 0)
            message = "This job is already in your list.";
        else if (updated.Count == 0)
            message = $"Added to {string.Join(", ", added)}.";
        else if (added.Count == 0)
            message = $"Updated {string.Join(", ", updated)}.";
        else
            message = $"Added to {string.Join(", ", added)}. " +
                      $"Updated {string.Join(", ", updated)}.";

        return Ok(new { message, added, updated, skipped });
    }

    // =====================================================
    // UPDATE (Admin) - category badli nahi ja sakti
    // =====================================================
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] JobPostDto dto)
    {
        var item = await _context.JobPosts
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Record not found." });

        var error = Validate(item.Category, dto);

        if (error != null)
            return BadRequest(new { message = error });

        // Admin ne kya badla - ye pata karne ke liye purani values
        var old = new JobSnapshot(
            item.ExamName,
            item.StartDate,
            item.LastDate,
            item.ExamDate,
            item.Syllabus,
            item.Link);

        Apply(item, dto);

        await _context.SaveChangesAsync();

        // Job (Latest Jobs) badli to jin users ne ise add kiya hai
        // unke Exam Form / Exam / Preparation / Result bhi update
        if (item.Category == JobPost.CategoryJob)
        {
            await SyncJobToUsersAsync(item, old);
            await SyncSectionsAsync(item, old);
        }

        HttpContext.Items[AuditLogFilter.ActionKey] =
            $"Edit {Label(item.Category)}";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            item.ExamName;

        return NoContent();
    }

    // =====================================================
    // PUBLISH / UNPUBLISH (Admin)  - ek click me Draft <-> Published
    // =====================================================
    [HttpPut("{id:int}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetPublished(
        int id,
        [FromBody] JobPublishDto dto)
    {
        var item = await _context.JobPosts
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Record not found." });

        // Adhuri entry student ko dikhne se rokne ke liye
        if (dto.Published)
        {
            if ((item.Category == JobPost.CategoryResult ||
                 item.Category == JobPost.CategoryAdmitCard) &&
                item.PostDate == null)
            {
                return BadRequest(new
                {
                    message = "Add the date (Edit) before publishing."
                });
            }

            if (item.Category == JobPost.CategoryExam &&
                string.IsNullOrWhiteSpace(item.Details))
            {
                return BadRequest(new
                {
                    message =
                        "Add the Details (video link) (Edit) before publishing."
                });
            }
        }

        item.IsDraft = !dto.Published;

        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] =
            $"{(dto.Published ? "Publish" : "Unpublish")} {Label(item.Category)}";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            item.ExamName;

        return NoContent();
    }

    // =====================================================
    // DELETE (Admin)
    // =====================================================
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.JobPosts
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item == null)
            return NotFound(new { message = "Record not found." });

        // Job delete hui to isse bane user records bhi hat jaate hain
        if (item.Category == JobPost.CategoryJob)
        {
            // Is job se bani Exam / Result / Admit Card entries bhi hat jaati hain
            _context.JobPosts.RemoveRange(
                await _context.JobPosts
                    .Where(x => x.SourceJobId == id)
                    .ToListAsync());

            _context.ExamForms.RemoveRange(
                await _context.ExamForms
                    .Where(x => x.SourceJobId == id)
                    .ToListAsync());

            _context.Exams.RemoveRange(
                await _context.Exams
                    .Where(x => x.SourceJobId == id)
                    .ToListAsync());

            _context.Preparations.RemoveRange(
                await _context.Preparations
                    .Where(x => x.SourceJobId == id)
                    .ToListAsync());

            _context.Results.RemoveRange(
                await _context.Results
                    .Where(x => x.SourceJobId == id)
                    .ToListAsync());
        }

        _context.JobPosts.Remove(item);
        await _context.SaveChangesAsync();

        HttpContext.Items[AuditLogFilter.ActionKey] =
            $"Delete {Label(item.Category)}";
        HttpContext.Items[AuditLogFilter.DetailsKey] =
            item.ExamName;

        return NoContent();
    }

    // =====================================================
    // HELPERS
    // =====================================================

    // Edit se pehle ki job values
    private sealed record JobSnapshot(
        string ExamName,
        DateOnly? StartDate,
        DateOnly? LastDate,
        DateOnly? ExamDate,
        string? Syllabus,
        string Link);

    // Job ka naam / link badla to Exam, Result, Admit Card ki entries me bhi badalta hai.
    // Jis cheez ko admin ne un sections me khud alag kar diya ho, wo waisi hi rehti hai.
    private async Task SyncSectionsAsync(JobPost job, JobSnapshot old)
    {
        var nameChanged = old.ExamName.Trim() != job.ExamName;
        var linkChanged = old.Link != job.Link;

        if (!nameChanged && !linkChanged)
            return;

        var children = await _context.JobPosts
            .Where(x => x.SourceJobId == job.Id)
            .ToListAsync();

        foreach (var child in children)
        {
            if (nameChanged &&
                child.ExamName.Trim() == old.ExamName.Trim())
            {
                child.ExamName = job.ExamName;
            }

            if (linkChanged &&
                child.Category != JobPost.CategoryExam &&
                child.Link == old.Link)
            {
                child.Link = job.Link;
            }
        }

        await _context.SaveChangesAsync();
    }

    // Admin ne job me jo badla, wahi un users ke records me bhi badalta hai
    // jinhone ye job add ki hai. Jo cheez admin ne nahi badli, wo user ki
    // apni edit ki hui value waisi hi rehti hai.
    // Purane records (jo link hone se pehle bane) purane naam se milte hain
    // aur is dauran job se jud jaate hain.
    private async Task SyncJobToUsersAsync(JobPost job, JobSnapshot old)
    {
        var oldKey = old.ExamName.Trim().ToLower();

        var nameChanged = !string.Equals(
            old.ExamName.Trim(),
            job.ExamName,
            StringComparison.Ordinal);

        var startChanged = old.StartDate != job.StartDate;
        var lastChanged = old.LastDate != job.LastDate;
        var examDateChanged = old.ExamDate != job.ExamDate;
        var syllabusChanged = old.Syllabus != job.Syllabus;
        var linkChanged = old.Link != job.Link;

        // ---- Exam Forms ----
        var forms = await _context.ExamForms
            .Where(x =>
                x.SourceJobId == job.Id ||
                (x.SourceJobId == null &&
                 x.ExamName.ToLower() == oldKey))
            .ToListAsync();

        foreach (var form in forms)
        {
            form.SourceJobId = job.Id;

            if (nameChanged) form.ExamName = job.ExamName;

            if (startChanged && job.StartDate != null)
                form.RegisterStartDate = job.StartDate.Value;

            if (lastChanged && job.LastDate != null)
                form.RegisterEndDate = job.LastDate.Value;

            if (linkChanged) form.Link = job.Link;
        }

        // ---- Exams ----
        var exams = await _context.Exams
            .Where(x =>
                x.SourceJobId == job.Id ||
                (x.SourceJobId == null &&
                 x.ExamName.ToLower() == oldKey))
            .ToListAsync();

        foreach (var exam in exams)
        {
            exam.SourceJobId = job.Id;

            if (nameChanged) exam.ExamName = job.ExamName;

            if (examDateChanged)
            {
                exam.ExamDate = job.ExamDate;
                exam.Status = ExamStatusHelper.FromDate(job.ExamDate);
            }
        }

        // ---- Preparations ----
        var preps = await _context.Preparations
            .Where(x =>
                x.SourceJobId == job.Id ||
                (x.SourceJobId == null &&
                 x.ExamName.ToLower() == oldKey))
            .ToListAsync();

        foreach (var prep in preps)
        {
            prep.SourceJobId = job.Id;

            if (nameChanged) prep.ExamName = job.ExamName;

            if (syllabusChanged) prep.Syllabus = job.Syllabus;
        }

        // ---- Results ----
        var results = await _context.Results
            .Where(x =>
                x.SourceJobId == job.Id ||
                (x.SourceJobId == null &&
                 x.ExamName.ToLower() == oldKey))
            .ToListAsync();

        foreach (var result in results)
        {
            result.SourceJobId = job.Id;

            if (nameChanged) result.ExamName = job.ExamName;
        }

        await _context.SaveChangesAsync();
    }

    private static string? NormalizeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Categories.FirstOrDefault(x =>
            string.Equals(
                x,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string Label(string category) =>
        category switch
        {
            JobPost.CategoryAdmitCard => "Admit Card",
            JobPost.CategoryResult => "Result",
            JobPost.CategoryExam => "Exam",
            _ => "Job"
        };

    private static string? Validate(string? category, JobPostDto dto)
    {
        if (category == null)
            return "Category must be Job, Result, AdmitCard or Exam.";

        if (string.IsNullOrWhiteSpace(dto.ExamName))
            return "Exam name is required.";

        // Exam section: sirf Exam Form (naam) + Details (YouTube link)
        if (category == JobPost.CategoryExam)
        {
            if (string.IsNullOrWhiteSpace(dto.Details))
                return "Details (YouTube link) is required.";

            if (!IsYouTubeUrl(dto.Details))
                return "Details must be a valid YouTube link.";

            return null;
        }

        // javascript: jaise links rokne ke liye sirf http / https
        if (!IsHttpUrl(dto.Link))
            return "Please enter a valid link starting with http:// or https://";

        // Details (YouTube video) teeno categories me optional hai,
        // par dena ho to YouTube link hi
        if (!string.IsNullOrWhiteSpace(dto.Details) &&
            !IsYouTubeUrl(dto.Details))
        {
            return "Details must be a valid YouTube link.";
        }

        if (category == JobPost.CategoryJob)
        {
            if (dto.StartDate == null || dto.LastDate == null)
                return "Start date and last date are required.";

            // Start date aur last date ka order ab check nahi hota:
            // job last date tak list me rehti hai aur last date tak hi add hoti hai.
        }
        else
        {
            if (dto.PostDate == null)
                return "Date is required.";

            if (string.IsNullOrWhiteSpace(dto.Status) ||
                !Statuses.Contains(dto.Status))
            {
                return "Status must be Out or Upcoming.";
            }
        }

        return null;
    }

    private static bool IsYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();

        return host == "youtu.be" ||
               host == "youtube.com" ||
               host.EndsWith(".youtube.com") ||
               host == "youtube-nocookie.com" ||
               host.EndsWith(".youtube-nocookie.com");
    }

    private static bool IsHttpUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp ||
               uri.Scheme == Uri.UriSchemeHttps;
    }

    // Category ke hisaab se sirf kaam ke fields set hote hain
    private static void Apply(JobPost item, JobPostDto dto)
    {
        item.ExamName = dto.ExamName.Trim();
        item.Link = item.Category == JobPost.CategoryExam
            ? string.Empty
            : (dto.Link ?? string.Empty).Trim();

        // Draft / Published (default Published)
        item.IsDraft = string.Equals(
            dto.PublishStatus?.Trim(),
            "Draft",
            StringComparison.OrdinalIgnoreCase);

        // Details (YouTube link): Job, Result, Admit Card aur Exam sabme
        item.Details = string.IsNullOrWhiteSpace(dto.Details)
            ? null
            : dto.Details.Trim();

        if (item.Category == JobPost.CategoryJob)
        {
            item.StartDate = dto.StartDate;
            item.LastDate = dto.LastDate;
            item.ExamDate = dto.ExamDate;
            item.Syllabus = string.IsNullOrWhiteSpace(dto.Syllabus)
                ? null
                : dto.Syllabus.Trim();
            item.PostDate = null;
            item.Status = null;
        }
        else if (item.Category == JobPost.CategoryExam)
        {
            item.StartDate = null;
            item.LastDate = null;
            item.ExamDate = null;
            item.Syllabus = null;
            item.PostDate = null;
            item.Status = null;
        }
        else
        {
            item.PostDate = dto.PostDate;
            item.Status = dto.Status!.Trim();
            item.StartDate = null;
            item.LastDate = null;
            item.ExamDate = null;
            item.Syllabus = null;
        }
    }
}