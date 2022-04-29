using System.Text;
using Kami.Configuration;
using Kami.Model;
using Kami.Serialization;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;

namespace Kami;

public interface IKamiClient
{
    Task<KamiUploadResult> UploadFile(byte[] file, string contentType, string fileName);
    Task<KamiDeleteResult> DeleteFile(string documentIdentifier);
    Task<KamiCreateViewSessionResult> CreateViewSession(string documentIdentifier, string userName, string userId, DateTime? expiresAt = null, KamiViewerOptions? viewerOptions = null, bool editable = true);
    Task<KamiDocumentExportResult> ExportFile(string documentIdentifier, string exportType = "inline");
}

public class KamiClient : IKamiClient
{
    private readonly HttpClient _httpClient;
    private readonly KamiOptions _kamiOptions;

    public KamiClient(HttpClient httpClient, IOptions<KamiOptions> kamiOptions)
    {
        _httpClient = httpClient;
        _kamiOptions = kamiOptions.Value;
    }

    private bool CheckFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(ext))
            return false;

        if (ext[0] == '.')
            ext = ext.Substring(1);

        return _kamiOptions.AllowedExtensions.Contains(ext.ToLower());
    }

    public async Task<KamiUploadResult> UploadFile(byte[] file, string contentType, string fileName)
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

        using (var multiPartContent = new MultipartFormDataContent(boundary))
        {
            var fileNameContent = new StringContent(fileName);
            fileNameContent.Headers.TryAddWithoutValidation("Content-Disposition", "form-data; name=\"name\"");
            multiPartContent.Add(fileNameContent);

            var documentContent = new ByteArrayContent(file);
            documentContent.Headers.TryAddWithoutValidation("Content-Disposition", $"form-data; name=\"document\"; filename=\"{fileName}\"");
            documentContent.Headers.TryAddWithoutValidation("Content-Type", contentType);
            multiPartContent.Add(documentContent);

            var response = await _httpClient.PostAsync("upload/embed/documents", multiPartContent);

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
                var data = response.Content.ReadAsStringAsync().Result;
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
    }

    public async Task<KamiDeleteResult> DeleteFile(string documentIdentifier)
    {
        using (var response = await _httpClient.DeleteAsync("embed/documents/" + documentIdentifier))
        {
            return new KamiDeleteResult
            {
                Success = response.IsSuccessStatusCode,
                Message = response.ReasonPhrase
            };
        }
    }

    public async Task<KamiCreateViewSessionResult> CreateViewSession(string documentIdentifier, string userName, string userId, DateTime? expiresAt = null, KamiViewerOptions? viewerOptions = null, bool editable = true)
    {
        var expirationDate = (expiresAt ?? DateTime.Now.AddYears(1)).ToString();
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

        var content = new StringContent(requestJson, Encoding.Default, "application/json");
        var response = await _httpClient.PostAsync("embed/sessions", content);

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
            var data = await response.Content.ReadAsStringAsync();
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

    public async Task<KamiDocumentExportResult> ExportFile(string documentIdentifier, string exportType = "inline")
    {
        var result = await CreateDocumentExport(documentIdentifier, exportType);

        while (result?.Status == "pending")
        {
            result = await GetDocumentExport(result.Id);
        }

        if (result?.Status == "done")
        {
            using (var client = new HttpClient())
            {
                result.FileBytes = await client.GetByteArrayAsync(result.FileUrl);
            }
        }

        return result ?? new KamiDocumentExportResult
        {
            Status = "error",
            ErrorType = "Unable to create document export"
        };
    }

    private async Task<KamiDocumentExportResult> CreateDocumentExport(string documentIdentifier, string exportType)
    {
        var requestJson = JsonConvert.SerializeObject(new
        {
            DocumentIdentifier = documentIdentifier,
            ExportType = exportType
        }, JsonSerialization.GetDefaultSerializerSettings());

        using (var content = new StringContent(requestJson, Encoding.Default, "application/json"))
        using (var response = await _httpClient.PostAsync("embed/exports", content))
        {
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
                var data = await response.Content.ReadAsStringAsync();
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

    private async Task<KamiDocumentExportResult> GetDocumentExport(string exportId)
    {
        using (var response = await _httpClient.GetAsync("embed/exports/" + exportId))
        {
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
                var data = await response.Content.ReadAsStringAsync();
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
}
