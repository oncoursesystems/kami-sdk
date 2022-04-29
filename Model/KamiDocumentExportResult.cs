namespace Kami.Model;

public class KamiDocumentExportResult
{
    public string Id { get; set; } = "";
    public string? Status { get; set; }
    public string? FileUrl { get; set; }
    public string? ErrorType { get; set; }
    public byte[]? FileBytes { get; set; }
}
