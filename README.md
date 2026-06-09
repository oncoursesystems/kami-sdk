![Kami logo](https://raw.githubusercontent.com/oncoursesystems/kami-sdk/master/kami.png)

# OnCourse.Kami

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://github.com/oncoursesystems/kami-sdk/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/oncoursesystems/kami-sdk/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/OnCourse.Kami)](https://www.nuget.org/packages/OnCourse.Kami/)

### OnCourse.Kami is a .NET SDK library used to communicate with the [Kami API](https://kamiembeddingapi.docs.apiary.io/)

## ✔ Features

Kami API library helps to generate requests for the following services:

- Embedding
  - Uploads
  - Documents
  - View Sessions
- Exporting

## ⭐ Installation

This project is a class library targeting .NET 8 and .NET 10.

To install the OnCourse.Kami NuGet package, run the following command via the dotnet CLI

```
dotnet add package OnCourse.Kami
```

Or run the following command in the Package Manager Console of Visual Studio

```
PM> Install-Package OnCourse.Kami
```

## 📕 General Usage

### Initialization

To use Kami, import the namespace and include the .AddKami() method when initializing the host builder (typically
found in the Program.cs file)

```csharp
using Kami;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKamiClient(builder.Configuration);

...

var app = builder.Build();

```

### Fault Handling / Resilience

The client is configured with a [Polly v8](https://github.com/App-vNext/Polly) resilience pipeline (via `Microsoft.Extensions.Http.Resilience`) that retries transient failures with exponential backoff and jitter, then caps the whole call with a timeout. Both are configured from the `Kami` settings section — by default, **3 retries** and a **100 second timeout** (see [Configuration](#configuration)). A timeout surfaces to callers as a `TaskCanceledException`.

For advanced scenarios you can customize the pipeline directly by passing a configuration action as the second parameter:

```csharp
using Polly;

builder.Services.AddKamiClient(builder.Configuration, pipeline =>
{
    pipeline.AddConcurrencyLimiter(10);
});
```

> **Note:** if you host with .NET Aspire `ServiceDefaults` (or otherwise call `AddStandardResilienceHandler` via `ConfigureHttpClientDefaults`), that global handler is *also* applied to the Kami client and will layer a second pipeline on top. To let this SDK own Kami's resilience, opt the client out of the global handler:
>
> ```csharp
> builder.Services.AddHttpClient(nameof(IKamiClient)).RemoveAllResilienceHandlers();
> ```

### Configuration

Additional configuration can be done in the appSettings.config file within the "Kami" section. The default settings are shown here and can be overridden if needed:

```json
{
  "Kami": {
    "Token": "Token #####################",
    "BaseAddress": "https://api.notablepdf.com/",
    "TimeoutSeconds": 100,
    "RetryCount": 3,
    "ExportPollIntervalSeconds": 2,
    "ExportMaxPollAttempts": 60,
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

## 🚀 Example

After initializing with the UseKami() method above, the client will be registered in the DI system. You can inject the client in the constructor of any class that needs to use it.

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
```
