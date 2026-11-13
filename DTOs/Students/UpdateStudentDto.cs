
using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs.Students;

public class UpdateStudentDto
{
    [Required(ErrorMessage = "Registration number is required.")]
    [StringLength(50, ErrorMessage = "Registration number cannot exceed 50 characters.")]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Student name is required.")]
    [StringLength(150, ErrorMessage = "Student name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string Email { get; set; } = string.Empty;
}
