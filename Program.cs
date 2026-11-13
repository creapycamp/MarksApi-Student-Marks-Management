using MarksApi.DTOs.Students;
using Microsoft.OpenApi;
using MarksApi.DTOs.Enrollments;
using MarksApi.DTOs.Reports;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MarksApi.DTOs.Courses;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using MarksApi.Data;
using MarksApi.Models;
using MarksApi.DTOs.Auth;

var builder = WebApplication.CreateBuilder(args);
// Problem Details for standardized API errors
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT Bearer token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});


// PostgreSQL connection
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());
// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");
// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience is missing.");

builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtKey)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

// Configure Authorization
builder.Services.AddAuthorization();

var app = builder.Build();
// Global exception handling
app.UseExceptionHandler();

// Standardized responses for empty HTTP error statuses
app.UseStatusCodePages();
// Health Check Endpoint
app.MapHealthChecks("/health");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();

app.UseAuthorization();

// Apply migrations and seed initial data
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await context.Database.MigrateAsync();

    await DatabaseSeeder.SeedAsync(
        context,
        app.Configuration
    );
}

// JWT Login Endpoint
app.MapPost("/api/auth/login",
    async (LoginRequestDto request,
           AppDbContext context,
           IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest("Username and password are required.");
    }

    // Find user
    var user = await context.Users
        .SingleOrDefaultAsync(u => u.Username == request.Username);

    if (user == null)
        return Results.Unauthorized();

    // Verify password
    var passwordHasher = new PasswordHasher<User>();

    var verificationResult = passwordHasher.VerifyHashedPassword(
        user,
        user.PasswordHash,
        request.Password
    );

    if (verificationResult == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    // Read JWT configuration
    var jwtKey = configuration["Jwt:Key"];
    var issuer = configuration["Jwt:Issuer"];
    var audience = configuration["Jwt:Audience"];

    if (string.IsNullOrWhiteSpace(jwtKey) ||
        string.IsNullOrWhiteSpace(issuer) ||
        string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException(
            "JWT configuration is missing.");
    }

    // Token expiration: 60 minutes
    var expiresAt = DateTime.UtcNow.AddMinutes(60);

    // Create token claims
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role)
    };

    if (user.StudentId.HasValue)
    {
        claims.Add(new Claim(
            "student_id",
            user.StudentId.Value.ToString()
        ));
    }

    // Sign JWT token
    var key = new SymmetricSecurityKey(
        Convert.FromBase64String(jwtKey)
    );

    var credentials = new SigningCredentials(
        key,
        SecurityAlgorithms.HmacSha256
    );

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: expiresAt,
        signingCredentials: credentials
    );

    var tokenString = new JwtSecurityTokenHandler()
        .WriteToken(token);

    // Return login response
    var response = new LoginResponseDto
    {
        Token = tokenString,
        Username = user.Username,
        Role = user.Role,
        StudentId = user.StudentId,
        ExpiresAtUtc = expiresAt
    };

    return Results.Ok(response);
})

.AllowAnonymous();

app.MapGet("/", () =>
    "Student Marks Management API is Running!");
app.MapGet("/api/teacher/test", () =>
{
    return Results.Ok("Welcome Teacher! You are authorized.");
})
.RequireAuthorization(policy => policy.RequireRole("Teacher"));

app.MapGet("/api/student/test", () =>
{
    return Results.Ok("Welcome Student! You are authorized.");
})
.RequireAuthorization(policy => policy.RequireRole("Student"));

var studentGroup = app.MapGroup("/api/students")
    .RequireAuthorization(policy =>
        policy.RequireRole("Teacher"));

// 1. CREATE STUDENT
studentGroup.MapPost("/", async (
    CreateStudentDto dto,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.RegistrationNo) ||
        string.IsNullOrWhiteSpace(dto.FullName) ||
        string.IsNullOrWhiteSpace(dto.Email))
    {
        return Results.BadRequest("All fields are required.");
    }

    var registrationNo = dto.RegistrationNo.Trim();
    var email = dto.Email.Trim().ToLowerInvariant();

    var exists = await db.Students.AnyAsync(s =>
        s.RegistrationNo == registrationNo ||
        s.Email == email);

    if (exists)
        return Results.Conflict("Registration number or email already exists.");

    var student = new Student
    {
        RegistrationNo = registrationNo,
        FullName = dto.FullName.Trim(),
        Email = email
    };

    db.Students.Add(student);
    await db.SaveChangesAsync();

    var response = new StudentResponseDto
    {
        Id = student.Id,
        RegistrationNo = student.RegistrationNo,
        FullName = student.FullName,
        Email = student.Email,
        CreatedAtUtc = student.CreatedAtUtc
    };

    return Results.Created($"/api/students/{student.Id}", response);
});

// 2. GET ALL STUDENTS

studentGroup.MapGet("/", async (
    AppDbContext db,
    int page = 1,
    int pageSize = 10,
    string? search = null,
    string? sortBy = "name",
    bool descending = false,
    CancellationToken cancellationToken = default) =>
{
    // Validate pagination
    if (page < 1)
        return Results.BadRequest("Page must be at least 1.");

    if (pageSize < 1 || pageSize > 50)
        return Results.BadRequest("Page size must be between 1 and 50.");

    // Start database query
    var query = db.Students.AsNoTracking();

    // Search students
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();

        query = query.Where(s =>
            s.FullName.Contains(term) ||
            s.RegistrationNo.Contains(term) ||
            s.Email.Contains(term));
    }

    // Count matching records before pagination
    var totalRecords = await query.CountAsync(cancellationToken);

    // Sorting
    query = sortBy?.ToLowerInvariant() switch
    {
        "registrationno" => descending
            ? query.OrderByDescending(s => s.RegistrationNo)
            : query.OrderBy(s => s.RegistrationNo),

        "email" => descending
            ? query.OrderByDescending(s => s.Email)
            : query.OrderBy(s => s.Email),

        "createdat" => descending
            ? query.OrderByDescending(s => s.CreatedAtUtc)
            : query.OrderBy(s => s.CreatedAtUtc),

        _ => descending
            ? query.OrderByDescending(s => s.FullName)
            : query.OrderBy(s => s.FullName)
    };

    // Pagination and DTO projection
    var students = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(s => new StudentResponseDto
        {
            Id = s.Id,
            RegistrationNo = s.RegistrationNo,
            FullName = s.FullName,
            Email = s.Email,
            CreatedAtUtc = s.CreatedAtUtc
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        Page = page,
        PageSize = pageSize,
        TotalRecords = totalRecords,
        TotalPages = (int)Math.Ceiling(
            totalRecords / (double)pageSize),
        Data = students
    });
});


// 3. GET STUDENT BY ID
studentGroup.MapGet("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var student = await db.Students
        .AsNoTracking()
        .Where(s => s.Id == id)
        .Select(s => new StudentResponseDto
        {
            Id = s.Id,
            RegistrationNo = s.RegistrationNo,
            FullName = s.FullName,
            Email = s.Email,
            CreatedAtUtc = s.CreatedAtUtc
        })
        .FirstOrDefaultAsync();

    return student is null
        ? Results.NotFound("Student not found.")
        : Results.Ok(student);
});

// 4. UPDATE STUDENT
studentGroup.MapPut("/{id:int}", async (
    int id,
    UpdateStudentDto dto,
    AppDbContext db) =>
{
    var student = await db.Students.FindAsync(id);

    if (student is null)
        return Results.NotFound("Student not found.");

    if (string.IsNullOrWhiteSpace(dto.RegistrationNo) ||
        string.IsNullOrWhiteSpace(dto.FullName) ||
        string.IsNullOrWhiteSpace(dto.Email))
    {
        return Results.BadRequest("All fields are required.");
    }

    var registrationNo = dto.RegistrationNo.Trim();
    var email = dto.Email.Trim().ToLowerInvariant();

    var duplicate = await db.Students.AnyAsync(s =>
        s.Id != id &&
        (s.RegistrationNo == registrationNo || s.Email == email));

    if (duplicate)
        return Results.Conflict("Registration number or email already exists.");

    student.RegistrationNo = registrationNo;
    student.FullName = dto.FullName.Trim();
    student.Email = email;

    await db.SaveChangesAsync();

    return Results.Ok("Student updated successfully.");
});

// 5. DELETE STUDENT
studentGroup.MapDelete("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var student = await db.Students.FindAsync(id);

    if (student is null)
        return Results.NotFound("Student not found.");

    var hasEnrollments = await db.Enrollments
        .AnyAsync(e => e.StudentId == id);

    var hasUserAccount = await db.Users
        .AnyAsync(u => u.StudentId == id);

    if (hasEnrollments || hasUserAccount)
        return Results.Conflict(
            "Student cannot be deleted because related records exist.");

    db.Students.Remove(student);
    await db.SaveChangesAsync();

    return Results.Ok("Student deleted successfully.");
});

var courseGroup = app.MapGroup("/api/courses")
    .RequireAuthorization(policy =>
        policy.RequireRole("Teacher"));

// 1. CREATE COURSE
courseGroup.MapPost("/", async (
    CreateCourseDto dto,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Code) ||
        string.IsNullOrWhiteSpace(dto.Title))
    {
        return Results.BadRequest("Course code and title are required.");
    }

    if (dto.CreditHours < 1 || dto.CreditHours > 4)
        return Results.BadRequest("Credit hours must be between 1 and 4.");

    var code = dto.Code.Trim().ToUpperInvariant();

    var exists = await db.Courses.AnyAsync(c => c.Code == code);

    if (exists)
        return Results.Conflict("Course code already exists.");

    var course = new Course
    {
        Code = code,
        Title = dto.Title.Trim(),
        CreditHours = dto.CreditHours
    };

    db.Courses.Add(course);
    await db.SaveChangesAsync();

    var response = new CourseResponseDto
    {
        Id = course.Id,
        Code = course.Code,
        Title = course.Title,
        CreditHours = course.CreditHours,
        CreatedAtUtc = course.CreatedAtUtc
    };

    return Results.Created($"/api/courses/{course.Id}", response);
});

// 2. GET ALL COURSES

courseGroup.MapGet("/", async (
    AppDbContext db,
    int page = 1,
    int pageSize = 10,
    string? search = null,
    string? sortBy = "title",
    bool descending = false,
    CancellationToken cancellationToken = default) =>
{
    if (page < 1)
        return Results.BadRequest(new { Message = "Page must be at least 1." });

    if (pageSize < 1 || pageSize > 50)
        return Results.BadRequest(new { Message = "Page size must be between 1 and 50." });

    var query = db.Courses.AsNoTracking();

    // Search by course title or code
    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim();

        query = query.Where(c =>
            c.Title.Contains(term) ||
            c.Code.Contains(term));
    }

    var totalRecords = await query.CountAsync(cancellationToken);

    // Sorting
    query = sortBy?.ToLowerInvariant() switch
    {
        "code" => descending
            ? query.OrderByDescending(c => c.Code)
            : query.OrderBy(c => c.Code),

        "credithours" => descending
            ? query.OrderByDescending(c => c.CreditHours)
            : query.OrderBy(c => c.CreditHours),

        "createdat" => descending
            ? query.OrderByDescending(c => c.CreatedAtUtc)
            : query.OrderBy(c => c.CreatedAtUtc),

        _ => descending
            ? query.OrderByDescending(c => c.Title)
            : query.OrderBy(c => c.Title)
    };

    var courses = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(c => new CourseResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            CreditHours = c.CreditHours,
            CreatedAtUtc = c.CreatedAtUtc
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        Page = page,
        PageSize = pageSize,
        TotalRecords = totalRecords,
        TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
        Data = courses
    });
});

// 3. GET COURSE BY ID
courseGroup.MapGet("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var course = await db.Courses
        .AsNoTracking()
        .Where(c => c.Id == id)
        .Select(c => new CourseResponseDto
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            CreditHours = c.CreditHours,
            CreatedAtUtc = c.CreatedAtUtc
        })
        .FirstOrDefaultAsync();

    return course is null
        ? Results.NotFound("Course not found.")
        : Results.Ok(course);
});

// 4. UPDATE COURSE
courseGroup.MapPut("/{id:int}", async (
    int id,
    UpdateCourseDto dto,
    AppDbContext db) =>
{
    var course = await db.Courses.FindAsync(id);

    if (course is null)
        return Results.NotFound("Course not found.");

    if (string.IsNullOrWhiteSpace(dto.Code) ||
        string.IsNullOrWhiteSpace(dto.Title))
    {
        return Results.BadRequest("Course code and title are required.");
    }

    if (dto.CreditHours < 1 || dto.CreditHours > 4)
        return Results.BadRequest("Credit hours must be between 1 and 4.");

    var code = dto.Code.Trim().ToUpperInvariant();

    var duplicate = await db.Courses.AnyAsync(c =>
        c.Id != id && c.Code == code);

    if (duplicate)
        return Results.Conflict("Course code already exists.");

    course.Code = code;
    course.Title = dto.Title.Trim();
    course.CreditHours = dto.CreditHours;

    await db.SaveChangesAsync();

    return Results.Ok("Course updated successfully.");
});

// 5. DELETE COURSE
courseGroup.MapDelete("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var course = await db.Courses.FindAsync(id);

    if (course is null)
        return Results.NotFound("Course not found.");

    var hasEnrollments = await db.Enrollments
        .AnyAsync(e => e.CourseId == id);

    if (hasEnrollments)
        return Results.Conflict(
            "Course cannot be deleted because enrollments exist.");

    db.Courses.Remove(course);
    await db.SaveChangesAsync();

    return Results.Ok("Course deleted successfully.");
});

var enrollmentGroup = app.MapGroup("/api/enrollments")
    .RequireAuthorization(policy =>
        policy.RequireRole("Teacher"));

// 1. CREATE ENROLLMENT
enrollmentGroup.MapPost("/", async (
    CreateEnrollmentDto dto,
    AppDbContext db) =>
{
    if (dto.StudentId <= 0 || dto.CourseId <= 0)
        return Results.BadRequest("Valid StudentId and CourseId are required.");

    var studentExists = await db.Students
        .AnyAsync(s => s.Id == dto.StudentId);

    if (!studentExists)
        return Results.NotFound("Student not found.");

    var courseExists = await db.Courses
        .AnyAsync(c => c.Id == dto.CourseId);

    if (!courseExists)
        return Results.NotFound("Course not found.");

    var alreadyEnrolled = await db.Enrollments.AnyAsync(e =>
        e.StudentId == dto.StudentId &&
        e.CourseId == dto.CourseId);

    if (alreadyEnrolled)
        return Results.Conflict("Student is already enrolled in this course.");

    var enrollment = new Enrollment
    {
        StudentId = dto.StudentId,
        CourseId = dto.CourseId,
        Mark = null
    };

    db.Enrollments.Add(enrollment);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/enrollments/{enrollment.Id}",
        new { enrollment.Id, enrollment.StudentId, enrollment.CourseId,
              enrollment.Mark, enrollment.EnrolledAtUtc });
});


// 2. GET ALL ENROLLMENTS (Pagination, Search & Sorting)
enrollmentGroup.MapGet("/", async (
    AppDbContext db,
    int page = 1,
    int pageSize = 10,
    string? search = null,
    string sortBy = "enrolledat",
    bool descending = true,
    CancellationToken cancellationToken = default) =>
{
    // Validate pagination
    if (page < 1)
        return Results.BadRequest("Page must be at least 1.");

    if (pageSize < 1 || pageSize > 50)
        return Results.BadRequest("PageSize must be between 1 and 50.");

    // Base query
    var query = db.Enrollments
        .AsNoTracking()
        .AsQueryable();

    // Search by student name, registration number,
    // course code, or course title
    if (!string.IsNullOrWhiteSpace(search))
    {
        search = search.Trim();

        query = query.Where(e =>
            e.Student.FullName.ToLower().Contains(search.ToLower()) ||
            e.Student.RegistrationNo.ToLower().Contains(search.ToLower()) ||
            e.Course.Code.ToLower().Contains(search.ToLower()) ||
            e.Course.Title.ToLower().Contains(search.ToLower()));
    }

    // Total records before pagination
    var totalRecords = await query.CountAsync(cancellationToken);

    // Sorting
    query = sortBy.ToLower() switch
    {
        "enrolledat" => descending
            ? query.OrderByDescending(e => e.EnrolledAtUtc)
            : query.OrderBy(e => e.EnrolledAtUtc),

        "studentid" => descending
            ? query.OrderByDescending(e => e.StudentId)
            : query.OrderBy(e => e.StudentId),

        "courseid" => descending
            ? query.OrderByDescending(e => e.CourseId)
            : query.OrderBy(e => e.CourseId),

        "mark" => descending
            ? query.OrderByDescending(e => e.Mark)
            : query.OrderBy(e => e.Mark),

        _ => query.OrderByDescending(e => e.EnrolledAtUtc)
    };

    // Pagination and DTO projection
    var enrollments = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(e => new EnrollmentResponseDto
        {
            Id = e.Id,
            StudentId = e.StudentId,
            StudentName = e.Student.FullName,
            CourseId = e.CourseId,
            CourseCode = e.Course.Code,
            CourseTitle = e.Course.Title,
            Mark = e.Mark,
            EnrolledAtUtc = e.EnrolledAtUtc
        })
        .ToListAsync(cancellationToken);

    var totalPages = (int)Math.Ceiling(
        totalRecords / (double)pageSize);

    return Results.Ok(new
    {
        Page = page,
        PageSize = pageSize,
        TotalRecords = totalRecords,
        TotalPages = totalPages,
        Data = enrollments
    });
});


// 3. GET ENROLLMENT BY ID
enrollmentGroup.MapGet("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var enrollment = await db.Enrollments
        .AsNoTracking()
        .Where(e => e.Id == id)
        .Select(e => new EnrollmentResponseDto
        {
            Id = e.Id,
            StudentId = e.StudentId,
            StudentName = e.Student.FullName,
            CourseId = e.CourseId,
            CourseCode = e.Course.Code,
            CourseTitle = e.Course.Title,
            Mark = e.Mark,
            EnrolledAtUtc = e.EnrolledAtUtc
        })
        .FirstOrDefaultAsync();

    return enrollment is null
        ? Results.NotFound("Enrollment not found.")
        : Results.Ok(enrollment);
});

// 4. UPDATE ENROLLMENT MARK
enrollmentGroup.MapPut("/{id:int}/mark", async (
    int id,
    UpdateEnrollmentMarkDto dto,
    AppDbContext db) =>
{
    if (dto.Mark.HasValue &&
        (dto.Mark.Value < 0 || dto.Mark.Value > 100))
    {
        return Results.BadRequest("Mark must be between 0 and 100.");
    }

    var enrollment = await db.Enrollments.FindAsync(id);

    if (enrollment is null)
        return Results.NotFound("Enrollment not found.");

    enrollment.Mark = dto.Mark;

    await db.SaveChangesAsync();

    return Results.Ok("Enrollment mark updated successfully.");
});

 // 6. GET COURSE AVERAGE
courseGroup.MapGet("/{courseId:int}/average", async (
    int courseId,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    var course = await db.Courses
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);

    if (course is null)
        return Results.NotFound("Course not found.");

    var gradedCount = await db.Enrollments
        .CountAsync(e =>
            e.CourseId == courseId && e.Mark.HasValue,
            cancellationToken);

    var average = await db.Enrollments
        .Where(e => e.CourseId == courseId && e.Mark.HasValue)
        .AverageAsync(e => (double?)e.Mark, cancellationToken);

    return Results.Ok(new
    {
        course.Id,
        course.Code,
        course.Title,
        AverageMark = average,
        GradedStudentCount = gradedCount
    });
});

 // 1. GET STUDENT TRANSCRIPT
app.MapGet("/api/students/{studentId:int}/transcript", async (
    int studentId,
    ClaimsPrincipal user,
    AppDbContext db,
    CancellationToken cancellationToken) =>
{
    // Students can only view their own transcript.
    if (user.IsInRole("Student"))
    {
        var studentIdClaim = user.FindFirst("student_id")?.Value;

        if (!int.TryParse(studentIdClaim, out var loggedInStudentId) ||
            loggedInStudentId != studentId)
        {
            return Results.Forbid();
        }
    }

    var student = await db.Students
        .AsNoTracking()
        .Where(s => s.Id == studentId)
        .Select(s => new
        {
            s.Id,
            s.FullName,
            s.RegistrationNo
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (student is null)
        return Results.NotFound("Student not found.");

    var courses = await db.Enrollments
        .AsNoTracking()
        .Where(e => e.StudentId == studentId)
        .Select(e => new TranscriptCourseDto
        {
            CourseCode = e.Course.Code,
            CourseTitle = e.Course.Title,
            CreditHours = e.Course.CreditHours,
            Mark = e.Mark
        })
        .ToListAsync(cancellationToken);

    // Only graded courses contribute to the weighted average.
    var gradedCourses = courses
        .Where(c => c.Mark.HasValue)
        .ToList();

    double? weightedAverage = null;

    if (gradedCourses.Count > 0)
    {
        var totalWeightedMarks = gradedCourses.Sum(
            c => c.Mark!.Value * c.CreditHours);

        var totalCreditHours = gradedCourses.Sum(
            c => c.CreditHours);

        weightedAverage = (double)totalWeightedMarks /
                          totalCreditHours;
    }

    var transcript = new StudentTranscriptDto
    {
        StudentId = student.Id,
        StudentName = student.FullName,
        RegistrationNo = student.RegistrationNo,
        Courses = courses,
        WeightedAverage = weightedAverage
    };

    return Results.Ok(transcript);
})
.RequireAuthorization(policy =>
    policy.RequireRole("Teacher", "Student"));

// 5. DELETE ENROLLMENT
enrollmentGroup.MapDelete("/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var enrollment = await db.Enrollments.FindAsync(id);

    if (enrollment is null)
        return Results.NotFound("Enrollment not found.");

    db.Enrollments.Remove(enrollment);
    await db.SaveChangesAsync();

    return Results.Ok("Enrollment deleted successfully.");
});

app.Run();
