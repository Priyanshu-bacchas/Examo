using Microsoft.EntityFrameworkCore;

namespace Examo.Models;

public partial class ExamoDbContext : DbContext
{
    public ExamoDbContext()
    {
    }

    public ExamoDbContext(DbContextOptions<ExamoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Exam> Exams { get; set; }
    public virtual DbSet<ExamForm> ExamForms { get; set; }
    public virtual DbSet<Preparation> Preparations { get; set; }
    public virtual DbSet<Schedule> Schedules { get; set; }
    public virtual DbSet<Student> Students { get; set; }
    public virtual DbSet<Subject> Subjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // =========================
        // Students
        // =========================
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .HasColumnName("Name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasColumnName("Email")
                .HasMaxLength(150)
                .IsRequired();

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.Course)
                .HasColumnName("Course")
                .HasMaxLength(150);

            entity.Property(e => e.Age)
                .HasColumnName("Age");

            entity.Property(e => e.City)
                .HasColumnName("City")
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // =========================
        // ExamForms
        // =========================
        modelBuilder.Entity<ExamForm>(entity =>
        {
            entity.ToTable("ExamForms");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ExamName)
                .HasColumnName("ExamName")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.RegisterStartDate)
                .HasColumnName("RegisterStartDate")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.RegisterEndDate)
                .HasColumnName("RegisterEndDate")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.Link)
                .HasColumnName("Link")
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasMaxLength(20)
                .HasDefaultValue("Pending")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // =========================
        // Exams
        // =========================
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.ToTable("Exams");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ExamName)
                .HasColumnName("ExamName")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.ExamDate)
                .HasColumnName("ExamDate")
                .HasColumnType("date");

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasMaxLength(20)
                .HasDefaultValue("Coming Soon")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // =========================
        // Preparations
        // =========================
        modelBuilder.Entity<Preparation>(entity =>
        {
            entity.ToTable("Preparations");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ExamName)
                .HasColumnName("ExamName")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasMaxLength(50)
                .HasDefaultValue("Not Started")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // =========================
        // Schedules
        // =========================
        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("Schedules");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Subject)
                .HasColumnName("Subject")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.ScheduleDate)
                .HasColumnName("ScheduleDate")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.StartTime)
                .HasColumnName("StartTime")
                .HasColumnType("time")
                .IsRequired();

            entity.Property(e => e.EndTime)
                .HasColumnName("EndTime")
                .HasColumnType("time");

            entity.Property(e => e.Description)
                .HasColumnName("Description")
                .HasMaxLength(500);

            entity.Property(e => e.Lecture)
                .HasColumnName("Lecture");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // =========================
        // Subjects
        // =========================
        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.SubjectName)
                .HasColumnName("SubjectName")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("Status")
                .HasMaxLength(50)
                .HasDefaultValue("Not Started")
                .IsRequired();

            entity.Property(e => e.Materials)
                .HasColumnName("Materials");

            entity.Property(e => e.Pdf)
                .HasColumnName("Pdf")
                .HasMaxLength(500);

            entity.Property(e => e.Link)
                .HasColumnName("Link")
                .HasMaxLength(500);

            entity.Property(e => e.Lectures)
                .HasColumnName("Lectures")
                .HasDefaultValue(0)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}