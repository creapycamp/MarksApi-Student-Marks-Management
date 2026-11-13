
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MarksApi.Models;

namespace MarksApi.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        IConfiguration configuration)
    {
        // Avoid inserting duplicate seed records
        if (await context.Students.AnyAsync())
            return;

        var students = new List<Student>
        {
            new()
            {
                Id = 1,
                RegistrationNo = "STU001",
                FullName = "Ali Ahmed",
                Email = "ali@example.com"
            },
            new()
            {
                Id = 2,
                RegistrationNo = "STU002",
                FullName = "Sara Khan",
                Email = "sara@example.com"
            }
        };

        var courses = new List<Course>
        {
            new()
            {
                Id = 1,
                Code = "CS101",
                Title = "Programming Fundamentals",
                CreditHours = 3
            },
            new()
            {
                Id = 2,
                Code = "CS102",
                Title = "Object Oriented Programming",
                CreditHours = 3
            },
            new()
            {
                Id = 3,
                Code = "CS103",
                Title = "Database Systems",
                CreditHours = 4
            }
        };

        context.Students.AddRange(students);
        context.Courses.AddRange(courses);

        await context.SaveChangesAsync();

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = 1, CourseId = 1, Mark = 85 },
            new() { StudentId = 1, CourseId = 2, Mark = 78 },
            new() { StudentId = 1, CourseId = 3, Mark = null },
            new() { StudentId = 2, CourseId = 1, Mark = 90 },
            new() { StudentId = 2, CourseId = 2, Mark = 82 },
            new() { StudentId = 2, CourseId = 3, Mark = 75 }
        };

        context.Enrollments.AddRange(enrollments);

        var passwordHasher = new PasswordHasher<User>();

        var teacher = new User
        {
            Username = "teacher01",
            Role = "Teacher"
        };

        teacher.PasswordHash = passwordHasher.HashPassword(
            teacher,
            configuration["SeedUsers:TeacherPassword"]
                ?? throw new InvalidOperationException(
                    "Teacher seed password is missing.")
        );

        var studentUser1 = new User
        {
            Username = "student01",
            Role = "Student",
            StudentId = 1
        };

        studentUser1.PasswordHash = passwordHasher.HashPassword(
            studentUser1,
            configuration["SeedUsers:Student1Password"]
                ?? throw new InvalidOperationException(
                    "Student 1 seed password is missing.")
        );

        var studentUser2 = new User
        {
            Username = "student02",
            Role = "Student",
            StudentId = 2
        };

        studentUser2.PasswordHash = passwordHasher.HashPassword(
            studentUser2,
            configuration["SeedUsers:Student2Password"]
                ?? throw new InvalidOperationException(
                    "Student 2 seed password is missing.")
        );

        context.Users.AddRange(teacher, studentUser1, studentUser2);

        await context.SaveChangesAsync();
    }
}
