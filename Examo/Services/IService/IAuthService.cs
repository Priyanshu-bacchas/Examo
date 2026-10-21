using Examo.DTOs.Auth;

namespace Examo.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResultDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> VerifyEmailAsync(VerifyEmailDto dto);
    Task ResendOtpAsync(ResendOtpDto dto);

    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> VerifyLoginOtpAsync(VerifyEmailDto dto);
    Task<AuthResponseDto> FirebaseLoginAsync(FirebaseAuthDto dto);

    Task ForgotPasswordAsync(ForgotPasswordDto dto);
    Task ResetPasswordAsync(ResetPasswordWithOtpDto dto);
}
