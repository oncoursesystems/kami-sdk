# Kami API SDK (OnCourse.Kami)

Kami API library helps to generate requests for the following services:

 * Embedding
   * Uploads
   * Documents
   * View Sessions
 * Exporting

#### Kami API Documentation Website
https://kamiembeddingapi.docs.apiary.io/


## Nuget / Pipeline

This is a library published to the [OnCourse Nuget Feed](https://dev.azure.com/oncoursesystems/Public%20Packages/_packaging?_a=feed&feed=OnCourseFeed).  You will need the latest version of the [**Azure Artifacts credential provider**](https://dev.azure.com/oncoursesystems/Public%20Packages/_packaging?_a=connect&feed=OnCourseFeed) installed locally to be able to run a dotnet restore and the nuget.config file provided in the same page in order to reference the package in another project.

### Versioning 

Commits to master will automatically increment the patch number.  If you need to update the major/minor numbers, that is done in the azure-pipelines.yml file.  Any change to the major/minor numbers will automatically reset the patch number back to 0.

## Setup

### Initializing
To use Kami, import the namespace and include the .UseKami() method when initializing the host builder (typically 
found in the Program.cs file)

```csharp
using OnCourse.Kami;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Services.BuildServiceProvider();
var configuration = provider.GetRequiredService<IConfiguration>();

...

var app = builder.Build();

app.UseKami(configuration);
```

### Fault Handling / Resilience

By default, the client will be configured to retry a call up to three times with increasing waits between (1s, 5s, 10s).  If after the third call the service still returns an error then the call will be considered failed.  You can override this policy during the UseKami method by passing in a policy as the second parameter.  It is recommended to use [Polly](https://github.com/App-vNext/Polly), a 3rd-party library, that has a lot of options for creating policies

```csharp

app.UseKami(configuration, (p => p.WaitAndRetryAsync(new[]
{
    TimeSpan.FromSeconds(1),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(10)
}));

```

### Configuration
Additional configuration can be done in the appSettings.config file within the "Kami" section. The default settings are shown here and can be overridden if needed:

```json
{
    "Kami": {
        "Token": "Token fxnDs3BDz-B3fLjK-PGU",
        "BaseAddress": "https://api.notablepdf.com/",
        "AllowedExtensions": [
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
        ]
    }
}
```

## Usage examples

After initializing with the UseKami() method above, the client will be registered in the DI system.  You can inject the client in the constructor of any class that needs to use it.

```csharp
public class TestClass
{
    private readonly IKamiClient kamiClient

    public TestClass(IKamiClient kamiClient)
    {
        this.kamiClient = kamiClient;
    }

    public async Task<KamiUploadResult> UploadDocument(int fileId)
    {
        var (bytes, mimeType, fileName) = await this.GetFile(fileId);
        return await this.kamiClient.UploadFile(bytes, mimeType, fileName);
    }

    public async Task<KamiDeleteResult> DeleteDocument(string kamiDocumentId)
    {
        await this.kamiClient.DeleteFile(kamiDocumentId);
    }

    public async Task<KamiCreateViewSessionResult> CreateViewSession(string kamiDocumentId)
    {
        var (username, userId) = await this.GetUser();

        // optional settings
        var expiresAt = DateTime.Now.AddDays(7);
        var viewerOptions = new KamiViewerOptions();
        // var mobileViewerOptions = KamiViewerOptions.Mobile;
        var editable = true;

        return await this.kamiClient.CreateViewSession(kamiDocumentId, username, userId, expiresAt, viewerOptions, editable);
    }

    public async Task<KamiDocumentExportResult> ExportDocument(kamiDocumentId)
    {
        // export type is optional, defaults to "inline". See Kami API documentation site for more options and what they do
        var exportType = "inline";

        return await this.kamiClient.ExportFile(kamiDocumentId, exportType);
    }
}
