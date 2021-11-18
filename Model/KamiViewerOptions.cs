namespace OnCourse.Kami.Model;

public class KamiViewerOptions
{
    public string Theme { get; set; } = "dark";
    public bool ShowSave { get; set; } = true;
    public bool ShowPrint { get; set; } = true;
    public bool ShowHelp { get; set; } = true;
    public bool ShowMenu { get; set; } = true;
    public KamiToolVisibility ToolVisibility { get; set; } = new KamiToolVisibility();
}

public class KamiViewerMobileOptions : KamiViewerOptions
{
    public KamiViewerMobileOptions()
    {
        ShowPrint = false;
        ShowMenu = false;
        ShowHelp = false;
        ToolVisibility = new KamiToolVisibility
        {
            Equation = false,
            Comment = false,
            Autograph = false
        };
    }
}

public class KamiToolVisibility
{
    public bool Normal { get; set; } = true;
    public bool Highlight { get; set; } = true;
    public bool Strikethrough { get; set; } = true;
    public bool Underline { get; set; } = true;
    public bool Comment { get; set; } = true;
    public bool Text { get; set; } = true;
    public bool Equation { get; set; } = true;
    public bool Drawing { get; set; } = true;
    public bool Shape { get; set; } = true;
    public bool Eraser { get; set; } = true;
    public bool Image { get; set; } = true;
    public bool Autograph { get; set; } = true;
    public bool Tts { get; set; } = true;
}
