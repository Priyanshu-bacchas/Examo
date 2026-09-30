using System;
using System.Collections.Generic;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=DESKTOP-7E71OKM\\MSSQLSERVER01;Database=Examo;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.Id)
                .HasName("PK__Exams__3214EC0763E353FB");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.ExamName)
                .HasMaxLength(200);

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Coming Soon");
        });
        modelBuilder.Entity<ExamForm>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ExamForm__3214EC0724B99665");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ExamName).HasMaxLength(200);
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
        });

        modelBuilder.Entity<Preparation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Preparat__3214EC07D874142C");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ExamName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Not Started");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Schedule__3214EC07723DE7C2");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Subject).HasMaxLength(150);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id)
                .HasName("PK__Subjects__3214EC071807C35B");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())");

            entity.Property(e => e.Link)
                .HasMaxLength(500);

            entity.Property(e => e.Pdf)
                .HasMaxLength(500);

            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Not Started");

            entity.Property(e => e.SubjectName)
                .HasMaxLength(150);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Subjects__3214EC071807C35B");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.Property(e => e.Pdf).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Not Started");
            entity.Property(e => e.SubjectName).HasMaxLength(150);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
