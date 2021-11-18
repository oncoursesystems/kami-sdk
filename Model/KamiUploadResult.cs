namespace OnCourse.Kami.Model;

public class KamiUploadResult
{
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FileStatus { get; set; }
    public string? DocumentIdentifier { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}
