using Examo.DTOs.Document;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IUserDocumentService _service;

    public DocumentsController(IUserDocumentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var documents = await _service.GetAllAsync();

        return Ok(documents.Select(DocumentResponseDto.From));
    }

    // multipart/form-data: name (optional) + file
    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [FromForm] string? name,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a file."
            });
        }

        try
        {
            var document =
                await _service.UploadAsync(name, file);

            return StatusCode(
                StatusCodes.Status201Created,
                DocumentResponseDto.From(document));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Storage (Supabase) ki dikkat
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = ex.Message });
        }
    }

    // File kholne ke liye temporary (10 minute) link
    [HttpGet("{id:int}/url")]
    public async Task<IActionResult> GetUrl(int id)
    {
        try
        {
            var url = await _service.GetUrlAsync(id);

            if (url == null)
            {
                return NotFound(new
                {
                    message = "Document not found."
                });
            }

            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = ex.Message });
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
                message = "Document not found."
            });
        }

        return NoContent();
    }
}