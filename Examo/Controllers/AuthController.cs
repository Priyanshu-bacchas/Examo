using Examo.DTOs.Auth;
using Examo.Services;
using Examo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examo.Controllers;

[ApiController]
[AllowAnonymous]
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
    // REGISTER  ->  OTP email jaata hai (token nahi milta)
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
    // VERIFY EMAIL (signup OTP)  ->  token milta hai
    // =====================================================

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailDto dto)
    {
        try
        {
            var result =
                await _authService
                    .VerifyEmailAsync(dto);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // RESEND OTP (signup verification)
    // =====================================================

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendOtpDto dto)
    {
        try
        {
            await _authService.ResendOtpAsync(dto);

            return Ok(new
            {
                message = "If the account exists, a new code has been sent."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // LOGIN
    // =====================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto)
    {
        try
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
        catch (EmailNotVerifiedException ex)
        {
            // Frontend isse OTP screen par le jaata hai
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    code = "EMAIL_NOT_VERIFIED",
                    email = ex.Email,
                    message = ex.Message
                });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Blocked account
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message = ex.Message
                });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // FORGOT PASSWORD (hamesha same response - email leak nahi)
    // =====================================================

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordDto dto)
    {
        try
        {
            await _authService.ForgotPasswordAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new
        {
            message =
                "If an account exists for this email, a reset code has been sent."
        });
    }

    // =====================================================
    // RESET PASSWORD (OTP + new password)
    // =====================================================

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordWithOtpDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);

            return Ok(new
            {
                message = "Password updated. You can log in now."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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