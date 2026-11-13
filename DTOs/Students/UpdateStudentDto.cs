namespace MarksApi.DTOs.Students;

public class UpdateStudentDto
{
    public string RegistrationNo { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}