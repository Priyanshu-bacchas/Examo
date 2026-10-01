using Examo.Models;
using Examo.Repositories;
using Examo.Repositories.Interfaces;
using Examo.Services;
using Examo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// DATABASE
// ===============================
builder.Services.AddDbContext<ExamoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ===============================
// REPOSITORIES
// ===============================
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IExamFormRepository, ExamFormRepository>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IPreparationRepository, PreparationRepository>();
builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

// ===============================
// SERVICES
// ===============================
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IExamFormService, ExamFormService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IPreparationService, PreparationService>();
builder.Services.AddScoped<ISubjectService, SubjectService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();

// ===============================
// CONTROLLERS
// ===============================
builder.Services.AddControllers();

// ===============================
// CORS
// ===============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ===============================
// SWAGGER
// ===============================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===============================
// SWAGGER
// ===============================
app.UseSwagger();
app.UseSwaggerUI();

// ===============================
// HTTPS
// ===============================
// Render already provides HTTPS at the public URL.
// Do not force HTTPS redirection inside the Render container.

// app.UseHttpsRedirection();

// ===============================
// CORS
// ===============================
app.UseCors("ReactApp");

// ===============================
// UPLOADED FILES
// ===============================
var uploadedFilesPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "UploadedFiles"
);

Directory.CreateDirectory(uploadedFilesPath);

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

// ===============================
// AUTHORIZATION
// ===============================
app.UseAuthorization();

// ===============================
// API CONTROLLERS
// ===============================
app.MapControllers();

// ===============================
// RUN
// ===============================
app.Run();