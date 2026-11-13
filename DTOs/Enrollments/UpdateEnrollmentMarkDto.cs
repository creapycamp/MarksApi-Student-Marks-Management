
using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs.Enrollments;

public class UpdateEnrollmentMarkDto
{
    [Range(0, 100, ErrorMessage = "Mark must be between 0 and 100.")]
    public int? Mark { get; set; }
}
