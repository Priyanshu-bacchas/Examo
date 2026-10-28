using Examo.Filters;
using Examo.DTOs.Subject;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireSubscription]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _service;

    public SubjectsController(ISubjectService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var subjects = await _service.GetAllAsync();

        return Ok(subjects);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var subject = await _service.GetByIdAsync(id);

        if (subject == null)
        {
            return NotFound(new
            {
                Message = "Subject not found."
            });
        }

        return Ok(subject);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        SubjectCreateDto dto)
    {
        var subject = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = subject.Id },
            subject);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        SubjectUpdateDto dto)
    {
        var subject = await _service.UpdateAsync(id, dto);

        if (subject == null)
        {
            return NotFound(new
            {
                Message = "Subject not found."
            });
        }

        return Ok(subject);
    }

    // multipart/form-data: file (sirf PDF)
    [HttpPost("{id:int}/pdf")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadPdf(
        int id,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                Message = "Please select a PDF file."
            });
        }

        try
        {
            var subject = await _service.UploadPdfAsync(id, file);

            if (subject == null)
            {
                return NotFound(new
                {
                    Message = "Subject not found."
                });
            }

            return Ok(subject);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            // Storage (Supabase) ki dikkat
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    Message = ex.Message
                });
        }
    }

    // PDF kholne ke liye temporary (10 minute) link
    [HttpGet("{id:int}/pdf-url")]
    public async Task<IActionResult> GetPdfUrl(int id)
    {
        try
        {
            var url = await _service.GetPdfUrlAsync(id);

            if (url == null)
            {
                return NotFound(new
                {
                    Message = "Subject not found."
                });
            }

            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new
            {
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    Message = ex.Message
                });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Subject not found."
            });
        }

        return NoContent();
    }
}