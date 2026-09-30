using Examo.DTOs.Exam;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly IExamService _service;

    public ExamsController(IExamService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _service.GetAllAsync();

        return Ok(exams);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var exam = await _service.GetByIdAsync(id);

        if (exam == null)
        {
            return NotFound(new
            {
                Message = "Exam not found."
            });
        }

        return Ok(exam);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        ExamCreateDto dto)
    {
        var exam = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = exam.Id },
            exam);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        ExamUpdateDto dto)
    {
        var exam = await _service.UpdateAsync(id, dto);

        if (exam == null)
        {
            return NotFound(new
            {
                Message = "Exam not found."
            });
        }

        return Ok(exam);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Exam not found."
            });
        }

        return NoContent();
    }
}