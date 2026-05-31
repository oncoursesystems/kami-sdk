using System.Text;
using OnCourse.Kami.Configuration;
using OnCourse.Kami.Model;
using OnCourse.Kami.Serialization;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace OnCourse.Kami;

public interface IKamiClient
{
    Task<KamiUploadResult> UploadFile(byte[] file, string contentType, string fileName, CancellationToken cancellationToken = default);
    Task<KamiDeleteResult> DeleteFile(string documentIdentifier, CancellationToken cancellationToken = default);
    Task<KamiCreateViewSessionResult> CreateViewSession(string documentIdentifier, string userName, string userId, DateTime? expiresAt = null, KamiViewerOptions? viewerOptions = null, bool editable = true, CancellationToken cancellationToken = default);
    Task<KamiDocumentExportResult> ExportFile(string documentIdentifier, string exportType = "inline", CancellationToken cancellationToken = default);
}

public class KamiClient : IKamiClient
{
    // Name of the unconfigured factory client used to download exported files from Kami's storage host.
    // It must NOT be the typed client, whose default Authorization header would leak to a foreign host.
    private const string ExportDownloadClientName = "KamiExportDownload";

    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KamiOptions _kamiOptions;

    public KamiClient(HttpClient httpClient, IHttpClientFactory httpClientFactory, IOptions<KamiOptions> kamiOptions)
    {
        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _kamiOptions = kamiOptions.Value;
    }

    private bool CheckFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(ext))
            return false;

        if (ext[0] == '.')
            ext = ext[1..];

        return _kamiOptions.AllowedExtensions.Contains(ext.ToLower());
    }

    public async Task<KamiUploadResult> UploadFile(byte[] file, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        const string boundary = "-----BOUNDARY";

        if (!CheckFileType(fileName))
        {
            return new KamiUploadResult
            {
                Success = false,
                Message = "File type is not supported"
            };
        }

        using var multiPartContent = new MultipartFormDataContent(boundary);
        var fileNameContent = new StringContent(fileName);
        fileNameContent.Headers.TryAddWithoutValidation("Content-Disposition", "form-data; name=\"name\"");
        multiPartContent.Add(fileNameContent);

        var documentContent = new ByteArrayContent(file);
        documentContent.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"document\"; filename=\"{fileName}\"");
        documentContent.Headers.TryAddWithoutValidation("Content-Type", contentType);
        multiPartContent.Add(documentContent);

        using var response = await _httpClient.PostAsync("upload/embed/documents", multiPartContent, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new KamiUploadResult
            {
                Success = false,
                Message = response.ReasonPhrase
            };
        }

        try
        {
            var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<KamiUploadResult>(data, JsonSerialization.GetDefaultSerializerSettings()) ?? new KamiUploadResult
            {
                Success = false,
                Message = "Could not deserialize upload result"
            };
        }
        catch (Exception ex)
        {
            return new KamiUploadResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<KamiDeleteResult> DeleteFile(string documentIdentifier, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("embed/documents/" + documentIdentifier, cancellationToken).ConfigureAwait(false);
        return new KamiDeleteResult
        {
            Success = response.IsSuccessStatusCode,
            Message = response.ReasonPhrase
        };
    }

    public async Task<KamiCreateViewSessionResult> CreateViewSession(string documentIdentifier, string userName, string userId, DateTime? expiresAt = null, KamiViewerOptions? viewerOptions = null, bool editable = true, CancellationToken cancellationToken = default)
    {
        var expirationDate = (expiresAt ?? DateTime.UtcNow.AddYears(1)).ToString(CultureInfo.InvariantCulture);
        var requestJson = JsonConvert.SerializeObject(new
        {
            DocumentIdentifier = documentIdentifier,
            User = new
            {
                Name = userName,
                UserId = userId
            },
            ExpiresAt = expirationDate,
            ViewerOptions = viewerOptions ?? new KamiViewerOptions(),
            Editable = editable
        }, JsonSerialization.GetDefaultSerializerSettings());

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("embed/sessions", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new KamiCreateViewSessionResult
            {
                Success = false,
                Message = response.ReasonPhrase
            };
        }

        try
        {
            var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var sessionResult = JsonConvert.DeserializeObject<KamiCreateViewSessionResult>(data, JsonSerialization.GetDefaultSerializerSettings());

            if (sessionResult == null)
            {
                return new KamiCreateViewSessionResult
                {
                    Success = false,
                    Message = "Could not deserialize create view session result"
                };
            }

            sessionResult.ExpirationDate = expirationDate;
            return sessionResult;
        }
        catch (Exception ex)
        {
            return new KamiCreateViewSessionResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<KamiDocumentExportResult> ExportFile(string documentIdentifier, string exportType = "inline", CancellationToken cancellationToken = default)
    {
        var result = await CreateDocumentExport(documentIdentifier, exportType, cancellationToken).ConfigureAwait(false);

        // Poll until the export finishes, with a delay between attempts and a hard cap so a stuck
        // export can't spin a tight, unbounded loop hammering the Kami API.
        var attempts = 0;
        while (result?.Status == "pending")
        {
            if (++attempts > _kamiOptions.ExportMaxPollAttempts)
            {
                return new KamiDocumentExportResult
                {
                    Status = "error",
                    ErrorType = "Timed out waiting for the document export to complete"
                };
            }

            await Task.Delay(TimeSpan.FromSeconds(_kamiOptions.ExportPollIntervalSeconds), cancellationToken).ConfigureAwait(false);
            result = await GetDocumentExport(result.Id, cancellationToken).ConfigureAwait(false);
        }

        if (result?.Status == "done")
        {
            // The exported file lives on a different (storage) host, so use a plain factory client rather
            // than the typed client whose Kami Authorization header would otherwise be sent to that host.
            var downloadClient = _httpClientFactory.CreateClient(ExportDownloadClientName);
            result.FileBytes = await downloadClient.GetByteArrayAsync(result.FileUrl, cancellationToken).ConfigureAwait(false);
        }

        return result ?? new KamiDocumentExportResult
        {
            Status = "error",
            ErrorType = "Unable to create document export"
        };
    }

    private async Task<KamiDocumentExportResult> CreateDocumentExport(string documentIdentifier, string exportType, CancellationToken cancellationToken)
    {
        var requestJson = JsonConvert.SerializeObject(new
        {
            DocumentIdentifier = documentIdentifier,
            ExportType = exportType
        }, JsonSerialization.GetDefaultSerializerSettings());

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("embed/exports", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = response.ReasonPhrase
            };
        }

        try
        {
            var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<KamiDocumentExportResult>(data, JsonSerialization.GetDefaultSerializerSettings()) ?? new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = "Could not deserialize document export result"
            };
        }
        catch (Exception ex)
        {
            return new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = ex.Message
            };
        }
    }

    private async Task<KamiDocumentExportResult> GetDocumentExport(string exportId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("embed/exports/" + exportId, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = response.ReasonPhrase
            };
        }

        try
        {
            var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<KamiDocumentExportResult>(data, JsonSerialization.GetDefaultSerializerSettings()) ?? new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = "Could not deserialize document export result"
            };
        }
        catch (Exception ex)
        {
            return new KamiDocumentExportResult
            {
                Status = "error",
                ErrorType = ex.Message
            };
        }
    }
}
