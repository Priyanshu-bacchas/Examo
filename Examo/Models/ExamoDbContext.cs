using Microsoft.EntityFrameworkCore;

namespace Examo.Models;

public partial class ExamoDbContext : DbContext
{
    public ExamoDbContext()
    {
    }

    public ExamoDbContext(
        DbContextOptions<ExamoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<ExamForm> ExamForms { get; set; }

    public virtual DbSet<Exam> Exams { get; set; }

    public virtual DbSet<Preparation> Preparations { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // =========================
        // STUDENTS
        // =========================

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id");

            entity.Property(e => e.Name)
                .HasColumnName("Name")
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.MobileNumber)
                .HasColumnName("MobileNumber")
                .HasMaxLength(50);

            entity.Property(e => e.Email)
                .HasColumnName("Email")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasColumnName("PasswordHash")
                .IsRequired();

            entity.Property(e => e.Role)
                .HasColumnName("Role")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Student");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("CreatedAt")
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.HasIndex(e => e.MobileNumber)
                .IsUnique()
                .HasFilter(
                    "\"MobileNumber\" IS NOT NULL");
        });

        // =========================
        // EXAM FORMS
        // =========================

        modelBuilder.Entity<ExamForm>(entity =>
        {
            entity.ToTable("ExamForms");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExamName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.RegisterStartDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.RegisterEndDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.Link)
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");
        });

        // =========================
        // EXAMS
        // =========================

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.ToTable("Exams");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExamName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.ExamDate)
                .HasColumnType("date");

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Coming Soon");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");
        });

        // =========================
        // PREPARATIONS
        // =========================

        modelBuilder.Entity<Preparation>(entity =>
        {
            entity.ToTable("Preparations");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExamName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Not Started");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");
        });

        // =========================
        // SCHEDULES
        // =========================

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("Schedules");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Subject)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.ScheduleDate)
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.StartTime)
                .HasColumnType("time")
                .IsRequired();

            entity.Property(e => e.EndTime)
                .HasColumnType("time");

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");
        });

        // =========================
        // SUBJECTS
        // =========================

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.ToTable("Subjects");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.SubjectName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue("Not Started");

            entity.Property(e => e.Lectures)
                .HasDefaultValue(0);

            entity.Property(e => e.Pdf)
                .HasMaxLength(500);

            entity.Property(e => e.Link)
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    "CURRENT_TIMESTAMP");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder);
}