using System.Security.Claims;
using System.Text;

using Examo.Filters;
using Examo.Models;
using Examo.Repositories;
using Examo.Repositories.Interfaces;
using Examo.Services;
using Examo.Services.Interfaces;

using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// DATABASE - NEON POSTGRESQL
// =====================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection is missing from configuration."
    );
}

// Safe diagnostic - password value is NEVER printed
var dbConnection =
    new NpgsqlConnectionStringBuilder(connectionString);

Console.WriteLine("========================================");
Console.WriteLine("NEON DATABASE CONFIGURATION");
Console.WriteLine($"Host: {dbConnection.Host}");
Console.WriteLine($"Port: {dbConnection.Port}");
Console.WriteLine($"Database: {dbConnection.Database}");
Console.WriteLine($"Username: {dbConnection.Username}");
Console.WriteLine(
    $"Password Length: {dbConnection.Password?.Length ?? 0}"
);
Console.WriteLine($"SSL Mode: {dbConnection.SslMode}");
Console.WriteLine("========================================");

builder.Services.AddDbContext<ExamoDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// =====================================================
// FIREBASE ADMIN
// =====================================================

var firebaseProjectId =
    builder.Configuration["Firebase:ProjectId"];

var firebaseClientEmail =
    builder.Configuration["Firebase:ClientEmail"];

var firebasePrivateKey =
    builder.Configuration["Firebase:PrivateKey"];

if (string.IsNullOrWhiteSpace(firebaseProjectId) ||
    string.IsNullOrWhiteSpace(firebaseClientEmail) ||
    string.IsNullOrWhiteSpace(firebasePrivateKey))
{
    throw new InvalidOperationException(
        "Firebase configuration is missing. " +
        "Check Firebase:ProjectId, Firebase:ClientEmail and Firebase:PrivateKey."
    );
}

firebasePrivateKey =
    firebasePrivateKey.Replace("\\n", "\n");

FirebaseApp.Create(new AppOptions
{
    Credential =
        GoogleCredential.FromServiceAccountCredential(
            new ServiceAccountCredential(
                new ServiceAccountCredential.Initializer(
                    firebaseClientEmail
                )
                {
                    ProjectId = firebaseProjectId
                }
                .FromPrivateKey(firebasePrivateKey)
            )
        )
});

// =====================================================
// REPOSITORIES
// =====================================================


builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IExamFormRepository, ExamFormRepository>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IPreparationRepository, PreparationRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

// =====================================================
// SERVICES
// =====================================================

// Logged-in user ki Id / role (JWT se) - data isolation ke liye
builder.Services.AddHttpContextAccessor();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Logs & Security
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IExamFormService, ExamFormService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();

// =====================================================
// JWT
// =====================================================

var jwtKey =
    builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key is missing from configuration."
    );
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ValidateIssuer = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidateAudience = true,

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                ValidateLifetime = true,

                ClockSkew = TimeSpan.Zero,

                RoleClaimType =
                    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",

                NameClaimType =
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            };

        // Blocked (ya delete hue) user ka purana token bhi kaam nahi karega
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var idValue =
                    context.Principal?
                        .FindFirstValue(ClaimTypes.NameIdentifier);

                if (!int.TryParse(idValue, out var userId))
                {
                    context.Fail("Invalid user.");
                    return;
                }

                var services = context.HttpContext.RequestServices;

                var cache =
                    services.GetRequiredService<IMemoryCache>();

                var cacheKey = $"user-denied:{userId}";

                if (!cache.TryGetValue(cacheKey, out bool denied))
                {
                    var db =
                        services.GetRequiredService<ExamoDbContext>();

                    var state = await db.Students
                        .AsNoTracking()
                        .Where(x => x.Id == userId)
                        .Select(x => (bool?)x.IsBlocked)
                        .FirstOrDefaultAsync();

                    // null = user delete ho chuka, true = blocked
                    denied = state != false;

                    cache.Set(
                        cacheKey,
                        denied,
                        TimeSpan.FromSeconds(30));
                }

                if (denied)
                {
                    context.Fail("Account blocked or removed.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers(options =>
{
    // Har endpoint par login zaroori (sirf AuthController [AllowAnonymous] hai)
    options.Filters.Add(new AuthorizeFilter());

    // Har successful POST/PUT/DELETE ko Logs & Security me likho
    options.Filters.Add<AuditLogFilter>();
});

// =====================================================
// CORS
// =====================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ReactApp",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

// =====================================================
// SWAGGER
// =====================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",

            Type =
                Microsoft.OpenApi.Models.SecuritySchemeType.Http,

            Scheme = "bearer",

            BearerFormat = "JWT",

            In =
                Microsoft.OpenApi.Models.ParameterLocation.Header,

            Description =
                "Enter: Bearer {your JWT token}"
        }
    );

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,

                            Id = "Bearer"
                        }
                },

                Array.Empty<string>()
            }
        }
    );
});

// =====================================================
// BUILD APP
// =====================================================

var app = builder.Build();

// =====================================================
// SWAGGER
// =====================================================

app.UseSwagger();

app.UseSwaggerUI();

// =====================================================
// CORS
// =====================================================

app.UseCors("ReactApp");

// =====================================================
// UPLOADED FILES
// =====================================================

var uploadedFilesPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "UploadedFiles"
    );

Directory.CreateDirectory(
    uploadedFilesPath
);

Directory.CreateDirectory(
    Path.Combine(
        uploadedFilesPath,
        "Subjects"
    )
);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =
            new PhysicalFileProvider(
                uploadedFilesPath
            ),

        RequestPath =
            "/UploadedFiles"
    }
);

// =====================================================
// AUTHENTICATION
// =====================================================

app.UseAuthentication();

app.UseAuthorization();

// =====================================================
// CONTROLLERS
// =====================================================

app.MapControllers();

// =====================================================
// RUN
// =====================================================

app.Run();