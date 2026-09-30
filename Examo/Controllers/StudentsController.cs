using Examo.DTOs.Student;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _service;

    public StudentsController(IStudentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await _service.GetAllAsync();

        return Ok(students);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var student = await _service.GetByIdAsync(id);

        if (student == null)
        {
            return NotFound(new
            {
                Message = "Student not found."
            });
        }

        return Ok(student);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        StudentCreateDto dto)
    {
        var student = await _service.AddAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = student.Id },
            student);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        StudentUpdateDto dto)
    {
        var student = await _service.UpdateAsync(id, dto);

        if (student == null)
        {
            return NotFound(new
            {
                Message = "Student not found."
            });
        }

        return Ok(student);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                Message = "Student not found."
            });
        }

        return NoContent();
    }
}