using Examo.DTOs.Subject;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Examo.Services;

public class SubjectService : ISubjectService
{
    private const long MaxPdfBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ISubjectRepository _repository;
    private readonly ISupabaseStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public SubjectService(
        ISubjectRepository repository,
        ISupabaseStorageService storage,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _storage = storage;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<Subject>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Subject?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Subject> AddAsync(SubjectCreateDto dto)
    {
        // Pdf sirf upload endpoint se set hota hai (client ka path nahi mante)
        var subject = new Subject
        {
            SubjectName = dto.SubjectName,
            Status = dto.Status,
            Materials = dto.Materials,
            Lectures = dto.Lectures,
            Link = dto.Link
        };

        return await _repository.AddAsync(subject);
    }

    public async Task<Subject?> UpdateAsync(
        int id,
        SubjectUpdateDto dto)
    {
        var subject = await _repository.GetByIdAsync(id);

        if (subject == null)
        {
            return null;
        }

        subject.SubjectName = dto.SubjectName;
        subject.Status = dto.Status;
        subject.Materials = dto.Materials;
        subject.Lectures = dto.Lectures;
        subject.Link = dto.Link;

        // Pdf yahan nahi badalta: edit karne par PDF link gayab nahi hota

        return await _repository.UpdateAsync(subject);
    }

    public async Task<Subject?> UploadPdfAsync(
        int id,
        IFormFile file)
    {
        var subject = await _repository.GetByIdAsync(id);

        if (subject == null)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(
                extension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only PDF files are allowed.");
        }

        if (!string.Equals(
                file.ContentType,
                "application/pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only PDF files are allowed.");
        }

        if (file.Length > MaxPdfBytes)
        {
            throw new ArgumentException(
                "PDF is too large. Maximum size is 10 MB.");
        }

        var path =
            $"users/{_currentUser.UserId}/subjects/{subject.Id}/" +
            $"{Guid.NewGuid():N}.pdf";

        await using (var stream = file.OpenReadStream())
        {
            await _storage.UploadAsync(
                path,
                stream,
                "application/pdf");
        }

        var oldPath = subject.Pdf;

        subject.Pdf = path;

        Subject updated;

        try
        {
            updated = await _repository.UpdateAsync(subject);
        }
        catch
        {
            await _storage.DeleteAsync(path);
            throw;
        }

        // Purani PDF Supabase se hata do (purane local path ko chhod do)
        if (IsStoragePath(oldPath))
        {
            await _storage.DeleteAsync(oldPath!);
        }

        return updated;
    }

    public async Task<string?> GetPdfUrlAsync(int id)
    {
        var subject = await _repository.GetByIdAsync(id);

        if (subject == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(subject.Pdf))
        {
            throw new ArgumentException(
                "No PDF uploaded for this subject.");
        }

        // Sirf is user ke apne folder ki file ka link banta hai
        if (!subject.Pdf.StartsWith(
                $"users/{_currentUser.UserId}/",
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "This PDF is no longer available. " +
                "Please upload it again.");
        }

        return await _storage.CreateSignedUrlAsync(subject.Pdf);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var subject = await _repository.GetByIdAsync(id);

        if (subject == null)
        {
            return false;
        }

        var pdfPath = subject.Pdf;

        var deleted = await _repository.DeleteAsync(id);

        if (deleted && IsStoragePath(pdfPath))
        {
            await _storage.DeleteAsync(pdfPath!);
        }

        return deleted;
    }

    private static bool IsStoragePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith("users/", StringComparison.Ordinal);
}
