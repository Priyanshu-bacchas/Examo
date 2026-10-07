using Examo.DTOs.Student;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(
        IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students =
            await _studentService.GetAllAsync();

        return Ok(
            students.Select(StudentResponseDto.From));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id)
    {
        var student =
            await _studentService.GetByIdAsync(id);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        return Ok(StudentResponseDto.From(student));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] StudentCreateDto dto)
    {
        try
        {
            var student =
                await _studentService.CreateAsync(dto);

            return StatusCode(
                StatusCodes.Status201Created,
                StudentResponseDto.From(student));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] StudentUpdateDto dto)
    {
        try
        {
            var student =
                await _studentService
                    .UpdateAsync(id, dto);

            if (student == null)
            {
                return NotFound(new
                {
                    message = "Student not found."
                });
            }

            return Ok(StudentResponseDto.From(student));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var deleted =
            await _studentService
                .DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        return NoContent();
    }
}