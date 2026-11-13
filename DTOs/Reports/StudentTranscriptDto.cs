namespace MarksApi.DTOs.Reports;

public class StudentTranscriptDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RegistrationNo { get; set; } = string.Empty;

    public List<TranscriptCourseDto> Courses { get; set; } = new();

    public double? WeightedAverage { get; set; }
}