
namespace MarksApi.Models;

public class Enrollment
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int CourseId { get; set; }

    public int? Mark { get; set; }

    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Student Student { get; set; } = null!;

    public Course Course { get; set; } = null!;
}
