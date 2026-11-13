
using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs.Enrollments;

public class CreateEnrollmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Student ID must be greater than zero.")]
    public int StudentId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Course ID must be greater than zero.")]
    public int CourseId { get; set; }
}
