using Examo.Models;
using Examo.Repositories;
using Examo.Repositories.Interfaces;
using Examo.Services;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Database - Neon PostgreSQL
// =========================
builder.Services.AddDbContext<ExamoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// =========================
// Repositories
// =========================
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IExamFormRepository, ExamFormRepository>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IPreparationRepository, PreparationRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

// =========================
// Services
// =========================
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IExamFormService, ExamFormService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();

builder.Services.AddControllers();

// =========================
// CORS
// =========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// =========================
// Swagger
// =========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =========================
// Swagger
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("ReactApp");

// =========================
// Uploaded Files
// =========================
var uploadedFilesPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "UploadedFiles"
);

Directory.CreateDirectory(
    Path.Combine(uploadedFilesPath, "Subjects")
);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadedFilesPath),
        RequestPath = "/UploadedFiles"
    }
);

app.UseAuthorization();

app.MapControllers();

app.Run();