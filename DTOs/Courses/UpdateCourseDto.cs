
using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs.Courses;

public class UpdateCourseDto
{
    [Required(ErrorMessage = "Course code is required.")]
    [StringLength(20, ErrorMessage = "Course code cannot exceed 20 characters.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Course title is required.")]
    [StringLength(150, ErrorMessage = "Course title cannot exceed 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Range(1, 4, ErrorMessage = "Credit hours must be between 1 and 4.")]
    public int CreditHours { get; set; }
}
