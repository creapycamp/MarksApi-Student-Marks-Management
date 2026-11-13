
using Microsoft.EntityFrameworkCore;
using MarksApi.Models;

namespace MarksApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            // Registration number must be unique
            entity.HasIndex(s => s.RegistrationNo)
                  .IsUnique();

            // Email must be unique
            entity.HasIndex(s => s.Email)
                  .IsUnique();

            // Required fields
            entity.Property(s => s.RegistrationNo)
                  .IsRequired();

            entity.Property(s => s.FullName)
                  .IsRequired();

            entity.Property(s => s.Email)
                  .IsRequired();

            // Email must be lowercase
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "ck_students_email_lowercase",
                    "email = lower(email)"
                ));
        });
     modelBuilder.Entity<Course>(entity =>
{
    // Course code must be unique
    entity.HasIndex(c => c.Code).IsUnique();

    // Required fields
    entity.Property(c => c.Code).IsRequired();
    entity.Property(c => c.Title).IsRequired();

    // Credit hours must be between 1 and 4
    entity.ToTable(table =>
        table.HasCheckConstraint(
            "ck_courses_credit_hours",
            "credit_hours >= 1 AND credit_hours <= 4"
        ));
});
modelBuilder.Entity<Enrollment>(entity =>
{
    // One student cannot enroll in the same course twice
    entity.HasIndex(e => new { e.StudentId, e.CourseId })
          .IsUnique();

    // Student relationship
    entity.HasOne(e => e.Student)
          .WithMany()
          .HasForeignKey(e => e.StudentId)
          .OnDelete(DeleteBehavior.Restrict);

    // Course relationship
    entity.HasOne(e => e.Course)
          .WithMany()
          .HasForeignKey(e => e.CourseId)
          .OnDelete(DeleteBehavior.Restrict);

    // Marks must be between 0 and 100, or NULL
    entity.ToTable(table =>
        table.HasCheckConstraint(
            "ck_enrollments_mark",
            "mark IS NULL OR (mark >= 0 AND mark <= 100)"
        ));
});
modelBuilder.Entity<User>(entity =>
{
    // Username must be unique
    entity.HasIndex(u => u.Username)
          .IsUnique();

    // Required fields
    entity.Property(u => u.Username).IsRequired();
    entity.Property(u => u.PasswordHash).IsRequired();
    entity.Property(u => u.Role).IsRequired();

    // Only Teacher and Student roles are allowed
    entity.ToTable(table =>
        table.HasCheckConstraint(
            "ck_users_role",
            "role IN ('Teacher', 'Student')"
        ));

    // Optional relationship with Student
    entity.HasOne(u => u.Student)
          .WithMany()
          .HasForeignKey(u => u.StudentId)
          .OnDelete(DeleteBehavior.Restrict);
});
    }
}
