using Examo.DTOs.Student;
using Examo.Filters;
using Examo.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Examo.Controllers;

// User & Role Management - sirf Admin
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public StudentsController(
        IStudentService studentService,
        ICurrentUserService currentUser,
        IMemoryCache cache)
    {
        _studentService = studentService;
        _currentUser = currentUser;
        _cache = cache;
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

            HttpContext.Items[AuditLogFilter.DetailsKey] =
                $"Added {student.Role} {student.Email}";

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
        // Admin apna khud ka Admin role hata kar lock-out na ho jaye
        if (id == _currentUser.UserId &&
            !string.IsNullOrWhiteSpace(dto.Role) &&
            !string.Equals(
                dto.Role.Trim(),
                "Admin",
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message =
                    "You cannot remove your own Admin role."
            });
        }

        try
        {
            var before =
                await _studentService.GetByIdAsync(id);

            var previousRole = before?.Role;

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

            var roleChange =
                previousRole != null &&
                previousRole != student.Role
                    ? $" (role {previousRole} -> {student.Role})"
                    : string.Empty;

            HttpContext.Items[AuditLogFilter.DetailsKey] =
                $"Updated {student.Email}{roleChange}";

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

    // Credentials Reset: user password bhool jaye to admin naya set karta hai
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordDto dto)
    {
        try
        {
            var student =
                await _studentService.ResetPasswordAsync(
                    id,
                    dto.NewPassword);

            if (student == null)
            {
                return NotFound(new
                {
                    message = "Student not found."
                });
            }

            HttpContext.Items[AuditLogFilter.DetailsKey] =
                $"Password reset for {student.Email}";

            return Ok(StudentResponseDto.From(student));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // Block / Unblock: blocked user login nahi kar sakta
    [HttpPost("{id:int}/block")]
    public async Task<IActionResult> SetBlocked(
        int id,
        [FromBody] BlockStudentDto dto)
    {
        if (id == _currentUser.UserId)
        {
            return BadRequest(new
            {
                message = "You cannot block your own account."
            });
        }

        var student =
            await _studentService.SetBlockedAsync(
                id,
                dto.IsBlocked);

        if (student == null)
        {
            return NotFound(new
            {
                message = "Student not found."
            });
        }

        // Token check ka cache hata do, taaki block turant lage
        _cache.Remove($"user-denied:{id}");

        HttpContext.Items[AuditLogFilter.ActionKey] =
            dto.IsBlocked ? "Block User" : "Unblock User";

        HttpContext.Items[AuditLogFilter.DetailsKey] =
            dto.IsBlocked
                ? $"Blocked {student.Email}"
                : $"Unblocked {student.Email}";

        return Ok(StudentResponseDto.From(student));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        if (id == _currentUser.UserId)
        {
            return BadRequest(new
            {
                message =
                    "You cannot delete your own account."
            });
        }

        var student =
            await _studentService.GetByIdAsync(id);

        var email = student?.Email;

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

        HttpContext.Items[AuditLogFilter.DetailsKey] =
            $"Deleted user {email}";

        return NoContent();
    }
}
