namespace Kami.Model;

public class KamiCreateViewSessionResult
{
    public string? ViewerUrl { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public string? ExpirationDate { get; set; }
}
