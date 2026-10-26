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
builder.Services.AddScoped<IExamResultRepository, ExamResultRepository>();
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

// System health: CPU / memory history (background sampling)
builder.Services.AddSingleton<SystemMetricsService>();

builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<SystemMetricsService>());

// Resend (email OTP)
builder.Services.Configure<ResendOptions>(
    builder.Configuration.GetSection("Resend"));

builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IExamFormService, ExamFormService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<IExamResultService, ExamResultService>();

// Notifications (exam form last date, exam reminders, result)
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddHostedService<NotificationSchedulerService>();

// Exam date nikalte hi Exams ka status database me "Done" ho jata hai
builder.Services.AddHostedService<ExamStatusSchedulerService>();

// Supabase Storage (Subjects PDF + Documents)
builder.Services.Configure<SupabaseOptions>(
    builder.Configuration.GetSection("Supabase"));

// Backup: agar Supabase:* na mile to SUPABASE_URL / SUPABASE_SECRET_KEY /
// SUPABASE_BUCKET environment variables bhi chalenge
builder.Services.PostConfigure<SupabaseOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.Url))
        options.Url =
            Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "";

    if (string.IsNullOrWhiteSpace(options.SecretKey))
        options.SecretKey =
            Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY") ?? "";

    var bucket = Environment.GetEnvironmentVariable("SUPABASE_BUCKET");

    if (!string.IsNullOrWhiteSpace(bucket) &&
        string.IsNullOrWhiteSpace(options.Bucket))
        options.Bucket = bucket;
});

builder.Services.AddHttpClient<
    ISupabaseStorageService,
    SupabaseStorageService>();

builder.Services.AddScoped<IUserDocumentRepository, UserDocumentRepository>();
builder.Services.AddScoped<IUserDocumentService, UserDocumentService>();
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
// SUPPORT MESSAGES TABLE (Help > Contact Us)
// Table na ho to apne aap ban jayegi, ho to kuch nahi hota.
// =====================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ExamoDbContext>();

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""SupportMessages"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""UserId"" integer NULL,
    ""Name"" varchar(100) NOT NULL,
    ""Email"" varchar(200) NOT NULL,
    ""Subject"" varchar(150) NOT NULL,
    ""Message"" varchar(3000) NOT NULL,
    ""Status"" varchar(20) NOT NULL DEFAULT 'Open',
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ""ResolvedAt"" timestamp with time zone NULL
);

ALTER TABLE ""SupportMessages"" ADD COLUMN IF NOT EXISTS ""AdminReply"" varchar(5000) NULL;
ALTER TABLE ""SupportMessages"" ADD COLUMN IF NOT EXISTS ""RepliedAt"" timestamp with time zone NULL;
ALTER TABLE ""SupportMessages"" ADD COLUMN IF NOT EXISTS ""RepliedBy"" varchar(150) NULL;");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Could not ensure SupportMessages table: " + ex.Message);
    }
}

// =====================================================
// NOTIFICATIONS TABLE
// Table na ho to apne aap ban jayegi, ho to kuch nahi hota.
// =====================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ExamoDbContext>();

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""Notifications"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""UserId"" integer NOT NULL,
    ""Type"" varchar(40) NOT NULL,
    ""Title"" varchar(200) NOT NULL,
    ""Message"" varchar(1000) NOT NULL,
    ""ExamName"" varchar(200) NULL,
    ""DedupeKey"" varchar(150) NOT NULL,
    ""IsRead"" boolean NOT NULL DEFAULT false,
    ""EmailSent"" boolean NOT NULL DEFAULT false,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE ""Notifications"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Notifications_User_Dedupe""
    ON ""Notifications"" (""UserId"", ""DedupeKey"");

CREATE INDEX IF NOT EXISTS ""IX_Notifications_User_Read""
    ON ""Notifications"" (""UserId"", ""IsRead"");");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Could not ensure Notifications table: " + ex.Message);
    }
}

// =====================================================
// JOBS TABLE (Jobs / Results / Admit Cards)
// Table na ho to apne aap ban jayegi, ho to kuch nahi hota.
// =====================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ExamoDbContext>();

        db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""JobPosts"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""Category"" varchar(20) NOT NULL,
    ""ExamName"" varchar(200) NOT NULL,
    ""StartDate"" date NULL,
    ""LastDate"" date NULL,
    ""ExamDate"" date NULL,
    ""Syllabus"" varchar(5000) NULL,
    ""PostDate"" date NULL,
    ""Status"" varchar(30) NULL,
    ""Link"" varchar(500) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

ALTER TABLE ""JobPosts"" ADD COLUMN IF NOT EXISTS ""ExamDate"" date NULL;
ALTER TABLE ""JobPosts"" ADD COLUMN IF NOT EXISTS ""Syllabus"" varchar(5000) NULL;

CREATE INDEX IF NOT EXISTS ""IX_JobPosts_Category""
    ON ""JobPosts"" (""Category"", ""Id"");");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Could not ensure JobPosts table: " + ex.Message);
    }
}

// =====================================================
// JOB LINK COLUMN (SourceJobId)
// Job se add hue records ko Admin Job se jodta hai.
// Column na ho to apne aap ban jayega, ho to kuch nahi hota.
// =====================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ExamoDbContext>();

        db.Database.ExecuteSqlRaw(@"
ALTER TABLE ""ExamForms"" ADD COLUMN IF NOT EXISTS ""SourceJobId"" integer NULL;
ALTER TABLE ""Exams"" ADD COLUMN IF NOT EXISTS ""SourceJobId"" integer NULL;
ALTER TABLE ""Preparations"" ADD COLUMN IF NOT EXISTS ""SourceJobId"" integer NULL;
ALTER TABLE ""Results"" ADD COLUMN IF NOT EXISTS ""SourceJobId"" integer NULL;");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Could not ensure SourceJobId columns: " + ex.Message);
    }
}

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