using Examo.DTOs.Auth;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(
        IAuthService authService)
    {
        _authService = authService;
    }

    // =====================================================
    // REGISTER
    // =====================================================

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDto dto)
    {
        try
        {
            var result =
                await _authService
                    .RegisterAsync(dto);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }

    // =====================================================
    // LOGIN
    // =====================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto)
    {
        var result =
            await _authService
                .LoginAsync(dto);

        if (result == null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email/mobile number or password."
            });
        }

        return Ok(result);
    }

    // =====================================================
    // FIREBASE LOGIN
    // =====================================================

    [HttpPost("firebase")]
    public async Task<IActionResult> Firebase(
        [FromBody] FirebaseAuthDto dto)
    {
        try
        {
            var result =
                await _authService
                    .FirebaseLoginAsync(dto);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}