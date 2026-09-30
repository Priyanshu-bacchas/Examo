using Examo.DTOs.Subject;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Examo.Services;

public class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _repository;
    private readonly IWebHostEnvironment _environment;

    public SubjectService(
        ISubjectRepository repository,
        IWebHostEnvironment environment)
    {
        _repository = repository;
        _environment = environment;
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
        var subject = new Subject
        {
            SubjectName = dto.SubjectName,
            Status = dto.Status,
            Materials = dto.Materials,
            Lectures = dto.Lectures,
            Pdf = dto.Pdf,
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
        subject.Pdf = dto.Pdf;
        subject.Link = dto.Link;

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

        var uploadFolder = Path.Combine(
            _environment.ContentRootPath,
            "UploadedFiles",
            "Subjects");

        Directory.CreateDirectory(uploadFolder);

        var uniqueFileName =
            $"{Guid.NewGuid():N}.pdf";

        var physicalFilePath = Path.Combine(
            uploadFolder,
            uniqueFileName);

        await using (var stream = new FileStream(
            physicalFilePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath =
            $"/UploadedFiles/Subjects/{uniqueFileName}";

        subject.Pdf = relativePath;

        return await _repository.UpdateAsync(subject);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _repository.DeleteAsync(id);
    }
}