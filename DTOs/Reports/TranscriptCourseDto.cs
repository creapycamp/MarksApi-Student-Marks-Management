namespace MarksApi.DTOs.Reports;

public class TranscriptCourseDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public int? Mark { get; set; }
}