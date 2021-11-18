namespace OnCourse.Kami.Configuration;

public class KamiOptions
{
    public const string SectionName = "Kami";

    public string? Token { get; set; }
    public string BaseAddress { get; set; } = "https://api.notablepdf.com/";
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
