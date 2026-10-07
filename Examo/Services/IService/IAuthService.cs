using Examo.DTOs.Auth;

namespace Examo.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> FirebaseLoginAsync(FirebaseAuthDto dto);
}