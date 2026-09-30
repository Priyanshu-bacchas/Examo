using Examo.DTOs.ExamForm;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamFormsController : ControllerBase
{
    private readonly IExamFormService _service;

    public ExamFormsController(IExamFormService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var examForms = await _service.GetAllAsync();

        return Ok(examForms);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var examForm = await _service.GetByIdAsync(id);

        if (examForm == null)
        {
            return NotFound(new
            {
                Message = "Exam form not found."
            });
        }

        return Ok(examForm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        ExamFormCreateDto dto)
    {
        if (dto.RegisterEndDate < dto.RegisterStartDate)
        {
            return BadRequest(new
            {
                Message = "Register end date cannot be earlier than start date."
            });
        }

        var examForm = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = examForm.Id },
            examForm);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        ExamFormUpdateDto dto)
    {
        if (dto.RegisterEndDate < dto.RegisterStartDate)
        {
            return BadRequest(new
            {
                Message = "Register end date cannot be earlier than start date."
            });
        }

        var examForm = await _service.UpdateAsync(id, dto);

        if (examForm == null)
        {
            return NotFound(new
            {
                Message = "Exam form not found."
            });
        }

        return Ok(examForm);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Exam form not found."
            });
        }

        return NoContent();
    }
}