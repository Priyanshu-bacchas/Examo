using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Examo.DTOs.Auth;
using Examo.Models;
using Examo.Repositories.Interfaces;
using Examo.Services.Interfaces;

using FirebaseAdmin.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Examo.Services;

public class AuthService : IAuthService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IConfiguration _configuration;
    private const string BlockedMessage =
        "Your account has been blocked. Please contact the admin.";

    private readonly IActivityLogService _activityLog;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<Student> _passwordHasher;

    // OTP rules
    private const int OtpValidMinutes = 10;
    private const int MaxOtpAttempts = 5;
    private const int ResendCooldownSeconds = 60;

    private const string PurposeVerify = "verify";
    private const string PurposeReset = "reset";
    private const string PurposeLogin = "login";

    public AuthService(
        IStudentRepository studentRepository,
        IConfiguration configuration,
        IActivityLogService activityLog,
        IEmailService emailService)
    {
        _studentRepository = studentRepository;
        _configuration = configuration;
        _activityLog = activityLog;
        _emailService = emailService;

        _passwordHasher =
            new PasswordHasher<Student>();
    }

    // =========================
    // REGISTER  (account banta hai, par OTP verify hone tak login nahi)
    // =========================

    public async Task<RegisterResultDto> RegisterAsync(
        RegisterDto dto)
    {
        var fullName =
            dto.FullName?.Trim();

        var mobile =
            dto.StudentId?.Trim();

        var email =
            dto.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException(
                "Full name is required.");

        if (string.IsNullOrWhiteSpace(mobile))
            throw new InvalidOperationException(
                "Mobile number is required.");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new InvalidOperationException(
                "Password is required.");

        var existing =
            await _studentRepository.GetByEmailAsync(email);

        // Verified account pehle se hai -> block
        if (existing != null && existing.EmailVerified)
        {
            throw new InvalidOperationException(
                "An account with this email already exists.");
        }

        Student student;

        if (existing != null)
        {
            // Pehle signup shuru kiya tha par OTP verify nahi kiya:
            // details update karke naya OTP bhejo (user atkega nahi).
            if (await _studentRepository
                .MobileExistsAsync(mobile, existing.Id))
            {
                throw new InvalidOperationException(
                    "An account with this mobile number already exists.");
            }

            existing.Name = fullName;
            existing.MobileNumber = mobile;
            existing.Password = dto.Password;
            existing.PasswordHash = string.Empty;

            student = existing;
        }
        else
        {
            if (await _studentRepository.MobileExistsAsync(mobile))
            {
                throw new InvalidOperationException(
                    "An account with this mobile number already exists.");
            }

            student = new Student
            {
                Name = fullName,
                MobileNumber = mobile,
                Email = email,

                // Public registration is always Student.
                Role = "Student",

                CreatedAt = DateTime.UtcNow,

                // Login password (StudentService jaisa hi: plain, hash nahi)
                Password = dto.Password,

                PasswordHash = string.Empty,

                // OTP verify hone tak false
                EmailVerified = false
            };

            student = await _studentRepository.AddAsync(student);
        }

        await IssueOtpAsync(student, PurposeVerify);

        await _activityLog.LogAsync(
            "Register",
            "Account created, verification code sent",
            student.Id,
            student.Name,
            student.Email,
            student.Role);

        return new RegisterResultDto
        {
            RequiresVerification = true,
            Email = student.Email,
            Message = "We have sent a 6-digit code to your email."
        };
    }

    // =========================
    // VERIFY EMAIL  (OTP sahi -> verified + login token)
    // =========================

    public async Task<AuthResponseDto> VerifyEmailAsync(
        VerifyEmailDto dto)
    {
        var student =
            await ConsumeOtpAsync(
                dto.Email,
                dto.Otp,
                PurposeVerify);

        if (student.IsBlocked)
            throw new UnauthorizedAccessException(BlockedMessage);

        student.EmailVerified = true;

        await _studentRepository.UpdateAsync(student);

        await _activityLog.LogAsync(
            "Email Verified",
            "Email verified with OTP",
            student.Id,
            student.Name,
            student.Email,
            student.Role);

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // RESEND OTP  (sirf unverified accounts ke liye)
    // =========================

    public async Task ResendOtpAsync(ResendOtpDto dto)
    {
        var student =
            await _studentRepository
                .GetByEmailAsync(dto.Email);

        // Account na ho / pehle se verified ho -> chup-chaap return
        // (taaki koi email check na kar sake)
        if (student == null ||
            student.EmailVerified ||
            student.IsBlocked)
        {
            return;
        }

        await IssueOtpAsync(student, PurposeVerify);
    }

    // =========================
    // LOGIN
    // =========================

    public async Task<AuthResponseDto?> LoginAsync(
        LoginDto dto)
    {
        var identifier =
            dto.Identifier?.Trim();

        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var student =
            await _studentRepository
                .GetByIdentifierAsync(identifier);

        if (student == null)
        {
            await _activityLog.LogAsync(
                "Login Failed",
                $"Unknown account: {identifier}",
                userName: identifier,
                success: false);

            return null;
        }

        if (!IsPasswordValid(student, dto.Password))
        {
            await _activityLog.LogAsync(
                "Login Failed",
                "Wrong password",
                student.Id,
                student.Name,
                student.Email,
                student.Role,
                success: false);

            return null;
        }

        if (student.IsBlocked)
        {
            await _activityLog.LogAsync(
                "Login Blocked",
                "Blocked account tried to log in",
                student.Id,
                student.Name,
                student.Email,
                student.Role,
                success: false);

            throw new UnauthorizedAccessException(
                BlockedMessage);
        }

        // Email verify nahi hui -> naya OTP bhejo, frontend OTP screen dikhayega
        if (!student.EmailVerified)
        {
            try
            {
                await IssueOtpAsync(student, PurposeVerify);
            }
            catch (InvalidOperationException ex)
                when (ex.Message.StartsWith("Please wait"))
            {
                // Cooldown chal raha hai: pehle wala OTP abhi valid hai
            }

            throw new EmailNotVerifiedException(student.Email);
        }

        // Har login par email OTP (Admin ko chhodkar, taaki lockout na ho).
        // Band karna ho: env var  Auth__LoginOtp = false
        var loginOtpOn =
            _configuration.GetValue<bool?>("Auth:LoginOtp") ?? true;

        if (loginOtpOn && student.Role != "Admin")
        {
            try
            {
                await IssueOtpAsync(student, PurposeLogin);
            }
            catch (InvalidOperationException ex)
                when (ex.Message.StartsWith("Please wait"))
            {
                // Cooldown: pehle bheja gaya login code abhi valid hai
            }

            throw new LoginOtpRequiredException(student.Email);
        }

        await _activityLog.LogAsync(
            "Login",
            "Logged in with password",
            student.Id,
            student.Name,
            student.Email,
            student.Role);

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // VERIFY LOGIN OTP  (password + email code dono sahi -> token)
    // =========================

    public async Task<AuthResponseDto> VerifyLoginOtpAsync(
        VerifyEmailDto dto)
    {
        var student =
            await ConsumeOtpAsync(
                dto.Email,
                dto.Otp,
                PurposeLogin);

        if (student.IsBlocked)
            throw new UnauthorizedAccessException(BlockedMessage);

        await _studentRepository.UpdateAsync(student);

        await _activityLog.LogAsync(
            "Login",
            "Logged in with password + email code",
            student.Id,
            student.Name,
            student.Email,
            student.Role);

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // FORGOT PASSWORD  (OTP email par)
    // =========================

    public async Task ForgotPasswordAsync(
        ForgotPasswordDto dto)
    {
        var student =
            await _studentRepository
                .GetByEmailAsync(dto.Email);

        // Account na ho / blocked ho -> chup-chaap return (same response)
        if (student == null || student.IsBlocked)
            return;

        try
        {
            await IssueOtpAsync(student, PurposeReset);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.StartsWith("Please wait"))
        {
            // Cooldown: dobara mail nahi bhejni, response same rahega
            return;
        }

        await _activityLog.LogAsync(
            "Forgot Password",
            "Password reset code requested",
            student.Id,
            student.Name,
            student.Email,
            student.Role);
    }

    // =========================
    // RESET PASSWORD  (OTP + naya password)
    // =========================

    public async Task ResetPasswordAsync(
        ResetPasswordWithOtpDto dto)
    {
        var student =
            await ConsumeOtpAsync(
                dto.Email,
                dto.Otp,
                PurposeReset);

        // Project ka existing system: plain Password column
        student.Password = dto.NewPassword;
        student.PasswordHash = string.Empty;

        // OTP email par aaya = email ownership prove ho gayi
        student.EmailVerified = true;

        await _studentRepository.UpdateAsync(student);

        await _activityLog.LogAsync(
            "Reset Password",
            "Password changed with email code",
            student.Id,
            student.Name,
            student.Email,
            student.Role);
    }

    // =========================
    // FIREBASE LOGIN
    // =========================

    public async Task<AuthResponseDto>
        FirebaseLoginAsync(FirebaseAuthDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.IdToken))
        {
            throw new InvalidOperationException(
                "Firebase ID token is required.");
        }

        FirebaseToken decodedToken;

        try
        {
            decodedToken =
                await FirebaseAuth
                    .DefaultInstance
                    .VerifyIdTokenAsync(dto.IdToken);
        }
        catch
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired Firebase authentication token.");
        }

        var email =
            decodedToken.Claims.TryGetValue(
                "email",
                out var emailValue)
                    ? emailValue?.ToString()
                    : null;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "Firebase account does not contain an email address.");
        }

        email = email.Trim().ToLowerInvariant();

        var firebaseName =
            decodedToken.Claims.TryGetValue(
                "name",
                out var nameValue)
                    ? nameValue?.ToString()
                    : null;

        var student =
            await _studentRepository
                .GetByEmailAsync(email);

        // =========================
        // NEW FIREBASE USER
        // =========================

        if (student == null)
        {
            var fullName =
                !string.IsNullOrWhiteSpace(dto.FullName)
                    ? dto.FullName.Trim()
                    : !string.IsNullOrWhiteSpace(firebaseName)
                        ? firebaseName.Trim()
                        : email.Split('@')[0];

            string? mobile = null;

            if (!string.IsNullOrWhiteSpace(dto.StudentId))
            {
                mobile = dto.StudentId.Trim();

                if (await _studentRepository
                    .MobileExistsAsync(mobile))
                {
                    throw new InvalidOperationException(
                        "An account with this mobile number already exists.");
                }
            }

            student = new Student
            {
                Name = fullName,
                MobileNumber = mobile,
                Email = email,
                Role = "Student",
                CreatedAt = DateTime.UtcNow
            };

            student.PasswordHash =
                _passwordHasher.HashPassword(
                    student,
                    GenerateRandomInternalValue());

            student =
                await _studentRepository
                    .AddAsync(student);
        }
        else
        {
            var changed = false;

            if (!string.IsNullOrWhiteSpace(dto.FullName) &&
                string.IsNullOrWhiteSpace(student.Name))
            {
                student.Name =
                    dto.FullName.Trim();

                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(dto.StudentId) &&
                string.IsNullOrWhiteSpace(student.MobileNumber))
            {
                var mobile =
                    dto.StudentId.Trim();

                if (await _studentRepository
                    .MobileExistsAsync(
                        mobile,
                        student.Id))
                {
                    throw new InvalidOperationException(
                        "An account with this mobile number already exists.");
                }

                student.MobileNumber = mobile;

                changed = true;
            }

            if (changed)
            {
                student =
                    await _studentRepository
                        .UpdateAsync(student);
            }
        }

        // Google ne email verify ki hui hoti hai.
        // Agar ye account pehle kisi ne is email se (bina verify kiye) bana
        // rakha tha, to uska password hata do (pre-registration attack se bachav).
        // User chahe to "Forgot Password" se apna password set kar sakta hai.
        if (!student.EmailVerified)
        {
            student.EmailVerified = true;
            student.Password = string.Empty;
            student.PasswordHash = string.Empty;
            student.OtpHash = null;
            student.OtpExpiresAt = null;

            student =
                await _studentRepository
                    .UpdateAsync(student);
        }

        if (student.IsBlocked)
        {
            await _activityLog.LogAsync(
                "Login Blocked",
                "Blocked account tried to log in with Google",
                student.Id,
                student.Name,
                student.Email,
                student.Role,
                success: false);

            throw new UnauthorizedAccessException(
                BlockedMessage);
        }

        await _activityLog.LogAsync(
            "Login",
            "Logged in with Google",
            student.Id,
            student.Name,
            student.Email,
            student.Role);

        return new AuthResponseDto
        {
            Token = GenerateToken(student),
            User = MapStudent(student)
        };
    }

    // =========================
    // PASSWORD CHECK
    // =========================

    // Naya system: plain Password column (admin reset isi ko badalta hai).
    // Purane accounts: PasswordHash se verify.
    private bool IsPasswordValid(
        Student student,
        string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (!string.IsNullOrEmpty(student.Password))
        {
            return student.Password == password;
        }

        if (string.IsNullOrWhiteSpace(student.PasswordHash))
            return false;

        try
        {
            return _passwordHasher.VerifyHashedPassword(
                student,
                student.PasswordHash,
                password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // =========================
    // OTP HELPERS
    // =========================

    // Purpose hash me mila diya hai: signup ka OTP password-reset me nahi chalega.
    private string HashOtp(
        string purpose,
        Student student,
        string otp)
    {
        var jwtKey =
            _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is missing from configuration.");

        using var hmac =
            new HMACSHA256(Encoding.UTF8.GetBytes(jwtKey));

        var hash =
            hmac.ComputeHash(
                Encoding.UTF8.GetBytes(
                    $"{purpose}:{student.Id}:{otp}"));

        return Convert.ToHexString(hash);
    }

    private async Task IssueOtpAsync(
        Student student,
        string purpose)
    {
        // Cooldown sirf tab, jab SAME purpose ka code abhi pending ho.
        // (Signup ke turant baad login karo to naya login code jaana chahiye.)
        var samePurposePending =
            !string.IsNullOrEmpty(student.OtpHash) &&
            student.OtpHash.StartsWith(purpose + "|");

        if (student.OtpSentAt.HasValue && samePurposePending)
        {
            var elapsed =
                (DateTime.UtcNow - student.OtpSentAt.Value)
                    .TotalSeconds;

            if (elapsed < ResendCooldownSeconds)
            {
                var wait =
                    (int)Math.Ceiling(ResendCooldownSeconds - elapsed);

                throw new InvalidOperationException(
                    $"Please wait {wait}s before requesting another code.");
            }
        }

        var otp =
            RandomNumberGenerator
                .GetInt32(0, 1_000_000)
                .ToString("D6");

        student.OtpHash = purpose + "|" + HashOtp(purpose, student, otp);
        student.OtpExpiresAt =
            DateTime.UtcNow.AddMinutes(OtpValidMinutes);
        student.OtpAttempts = 0;
        student.OtpSentAt = DateTime.UtcNow;

        await _studentRepository.UpdateAsync(student);

        await _emailService.SendOtpAsync(
            student.Email,
            student.Name,
            otp,
            purpose);
    }

    // OTP check karta hai. Sahi ho to OTP delete karke student return karta hai.
    private async Task<Student> ConsumeOtpAsync(
        string email,
        string otp,
        string purpose)
    {
        const string invalid = "Invalid or expired code.";

        var student =
            await _studentRepository
                .GetByEmailAsync(email);

        if (student == null ||
            string.IsNullOrEmpty(student.OtpHash) ||
            !student.OtpExpiresAt.HasValue)
        {
            throw new InvalidOperationException(invalid);
        }

        if (student.OtpExpiresAt.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "This code has expired. Please request a new one.");
        }

        if (student.OtpAttempts >= MaxOtpAttempts)
        {
            throw new InvalidOperationException(
                "Too many wrong attempts. Please request a new code.");
        }

        var expected = purpose + "|" + HashOtp(purpose, student, otp.Trim());

        var ok =
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(student.OtpHash));

        if (!ok)
        {
            student.OtpAttempts++;

            await _studentRepository.UpdateAsync(student);

            throw new InvalidOperationException(
                "Incorrect code. Please try again.");
        }

        // One-time use
        student.OtpHash = null;
        student.OtpExpiresAt = null;
        student.OtpAttempts = 0;

        return student;
    }

    // =========================
    // JWT
    // =========================

    private string GenerateToken(
        Student student)
    {
        var jwtKey =
            _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing from configuration.");
        }

        var issuer =
            _configuration["Jwt:Issuer"];

        var audience =
            _configuration["Jwt:Audience"];

        var claims =
            new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    student.Id.ToString()),

                new(
                    ClaimTypes.Name,
                    student.Name),

                new(
                    ClaimTypes.Email,
                    student.Email),

                new(
                    ClaimTypes.Role,
                    student.Role)
            };

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires:
                    DateTime.UtcNow.AddDays(7),
                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    // =========================
    // RESPONSE MAPPING
    // =========================

    private static AuthUserDto MapStudent(
        Student student)
    {
        return new AuthUserDto
        {
            Id = student.Id,
            FullName = student.Name,
            StudentId = student.MobileNumber,
            Email = student.Email,
            Role = student.Role
        };
    }

    private static string
        GenerateRandomInternalValue()
    {
        return Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
    }
}