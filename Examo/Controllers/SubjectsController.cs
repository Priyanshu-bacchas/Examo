using Examo.DTOs.Subject;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    [HttpPost("{id:int}/pdf")]
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