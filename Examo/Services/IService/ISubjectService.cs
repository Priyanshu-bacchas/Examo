using Examo.DTOs.Subject;
using Examo.Models;
using Microsoft.AspNetCore.Http;

namespace Examo.Services.Interfaces;

public interface ISubjectService
{
    Task<IEnumerable<Subject>> GetAllAsync();

    Task<Subject?> GetByIdAsync(int id);

    Task<Subject> AddAsync(SubjectCreateDto dto);

    Task<Subject?> UpdateAsync(
        int id,
        SubjectUpdateDto dto);

    Task<Subject?> UploadPdfAsync(
        int id,
        IFormFile file);

    // PDF kholne ka temporary link (sirf owner ke liye)
    Task<string?> GetPdfUrlAsync(int id);

    Task<bool> DeleteAsync(int id);
}
