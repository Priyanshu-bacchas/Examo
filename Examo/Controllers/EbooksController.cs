using Examo.Filters;
using Examo.Models;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EbooksController : ControllerBase
{
    private const long MaxPdfBytes = 50L * 1024 * 1024;
    private readonly ExamoDbContext _db;
    private readonly IEbookStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ISubscriptionService _subscriptions;

    public EbooksController(
        ExamoDbContext db,
        IEbookStorageService storage,
        ICurrentUserService currentUser,
        ISubscriptionService subscriptions)
    {
        _db = db;
        _storage = storage;
        _currentUser = currentUser;
        _subscriptions = subscriptions;
    }

    [HttpGet]
    [RequireSubscription]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? access,
        [FromQuery] bool includeDrafts = false)
    {
        var query = _db.Ebooks.AsNoTracking();

        if (!(_currentUser.IsAdmin && includeDrafts))
            query = query.Where(x => !x.IsDraft);

        if (!string.IsNullOrWhiteSpace(access))
        {
            if (access != "Free" && access != "Paid")
                return BadRequest(new { message = "Access must be Free or Paid." });

            query = query.Where(x => x.Access == access);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Access,
                PublishStatus = x.IsDraft ? "Draft" : "Published",
                x.SizeBytes,
                x.CreatedAt,
                Locked = x.Access == "Paid" && !_currentUser.IsAdmin
            })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxPdfBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string access,
        [FromForm] string publishStatus,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { message = "Please enter an ebook title." });
        if (title.Trim().Length > 200)
            return BadRequest(new { message = "Title cannot exceed 200 characters." });
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 1000)
            return BadRequest(new { message = "Description cannot exceed 1000 characters." });
        if (access != "Free" && access != "Paid")
            return BadRequest(new { message = "Access must be Free or Paid." });
        if (publishStatus != "Published" && publishStatus != "Draft")
            return BadRequest(new { message = "Visibility must be Published or Draft." });
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a PDF file." });
        if (file.Length > MaxPdfBytes)
            return BadRequest(new { message = "PDF size must be 50 MB or less." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are allowed." });

        await using (var stream = file.OpenReadStream())
        {
            var header = new byte[5];
            var read = await stream.ReadAsync(header.AsMemory(0, 5), cancellationToken);
            if (read != 5 || header[0] != '%' || header[1] != 'P' ||
                header[2] != 'D' || header[3] != 'F' || header[4] != '-')
                return BadRequest(new { message = "This file is not a valid PDF." });
        }

        var id = Guid.NewGuid().ToString("N");
        var path = $"ebooks/{id}.pdf";

        await using (var stream = file.OpenReadStream())
        {
            await _storage.UploadAsync(path, stream, "application/pdf", cancellationToken);
        }

        try
        {
            var ebook = new Ebook
            {
                Title = title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Access = access,
                IsDraft = publishStatus == "Draft",
                StoragePath = path,
                SizeBytes = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            _db.Ebooks.Add(ebook);
            await _db.SaveChangesAsync(cancellationToken);

            return Ok(new { ebook.Id, ebook.Title });
        }
        catch
        {
            await _storage.DeleteAsync(path, cancellationToken);
            throw;
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] EbookUpdateRequest request)
    {
        var ebook = await _db.Ebooks.FirstOrDefaultAsync(x => x.Id == id);
        if (ebook == null) return NotFound(new { message = "Ebook not found." });

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
            return BadRequest(new { message = "Enter a title up to 200 characters." });
        if (request.Description?.Length > 1000)
            return BadRequest(new { message = "Description cannot exceed 1000 characters." });
        if (request.Access != "Free" && request.Access != "Paid")
            return BadRequest(new { message = "Access must be Free or Paid." });
        if (request.PublishStatus != "Published" && request.PublishStatus != "Draft")
            return BadRequest(new { message = "Visibility must be Published or Draft." });

        ebook.Title = request.Title.Trim();
        ebook.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ebook.Access = request.Access;
        ebook.IsDraft = request.PublishStatus == "Draft";
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/access")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetAccess(int id, [FromBody] EbookAccessRequest request)
    {
        if (request.Access != "Free" && request.Access != "Paid")
            return BadRequest(new { message = "Access must be Free or Paid." });
        var ebook = await _db.Ebooks.FirstOrDefaultAsync(x => x.Id == id);
        if (ebook == null) return NotFound(new { message = "Ebook not found." });
        ebook.Access = request.Access;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetPublished(int id, [FromBody] EbookPublishRequest request)
    {
        var ebook = await _db.Ebooks.FirstOrDefaultAsync(x => x.Id == id);
        if (ebook == null) return NotFound(new { message = "Ebook not found." });
        ebook.IsDraft = !request.Published;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/url")]
    [RequireSubscription]
    public Task<IActionResult> GetUrl(int id) => CreateFileUrl(id, false);

    [HttpGet("{id:int}/download-url")]
    [RequireSubscription]
    public Task<IActionResult> GetDownloadUrl(int id) => CreateFileUrl(id, true);

    private async Task<IActionResult> CreateFileUrl(int id, bool download)
    {
        var ebook = await _db.Ebooks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDraft);
        if (ebook == null) return NotFound(new { message = "Ebook not found." });

        if (ebook.Access == "Paid" && !_currentUser.IsAdmin)
        {
            var status = await _subscriptions.GetStatusAsync(_currentUser.UserId);
            if (!status.HasAccess)
                return StatusCode(402, new { message = "An active subscription is required to open this ebook.", code = "SUBSCRIPTION_REQUIRED" });
        }

        var filename = $"{SanitizeFileName(ebook.Title)}.pdf";
        var url = await _storage.CreateSignedUrlAsync(
            ebook.StoragePath,
            600,
            download ? filename : null);
        return Ok(new { url });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ebook = await _db.Ebooks.FirstOrDefaultAsync(x => x.Id == id);
        if (ebook == null) return NotFound(new { message = "Ebook not found." });

        _db.Ebooks.Remove(ebook);
        await _db.SaveChangesAsync();
        await _storage.DeleteAsync(ebook.StoragePath);
        return NoContent();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "ebook" : clean;
    }
}

public sealed class EbookUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Access { get; set; } = "Free";
    public string PublishStatus { get; set; } = "Published";
}

public sealed class EbookAccessRequest
{
    public string Access { get; set; } = string.Empty;
}

public sealed class EbookPublishRequest
{
    public bool Published { get; set; }
}
