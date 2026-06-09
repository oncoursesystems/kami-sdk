namespace OnCourse.Kami.Configuration;

public class KamiOptions
{
    public const string SectionName = "Kami";

    public string? Token { get; set; }
    public string BaseAddress { get; set; } = "https://api.notablepdf.com/";

    /// <summary>
    /// Total time allowed for a single Kami API call (including retries) before it is cancelled.
    /// Document uploads and view-session creation can be slow, so this defaults generously.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Number of times a transient failure is retried (exponential backoff with jitter) before the call fails.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Seconds to wait between polls while an <c>ExportFile</c> document export is still pending.
    /// </summary>
    public int ExportPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Maximum number of times <c>ExportFile</c> polls for a pending export before giving up,
    /// so a stuck export cannot loop forever.
    /// </summary>
    public int ExportMaxPollAttempts { get; set; } = 60;

    public List<string> AllowedExtensions { get; set; } = new List<string>()
    {
        "doc",
        "docx",
        "ppt",
        "pptx",
        "xls",
        "xlsx",
        "pdf",
        "odt",
        "odp",
        "ods",
        "txt",
        "rtf",
        "gdoc",
        "gsheet",
        "jpg",
        "jpeg",
        "gif",
        "png",
        "tif",
        "tiff"
    };
}
