using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WatchCollection.Models;

namespace WatchCollection.Services
{
    public class JSONServices
    {
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });

        private const string BaseUrl = "http://185.157.245.38:8080/json";

        internal async Task<List<Watch>> GetWatchesAsync()
        {
            const string url = $"{BaseUrl}?FileName=MyWatches.json";
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<Watch>();
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<List<Watch>>(contentStream) ?? new List<Watch>();
        }

        internal async Task SetWatchesAsync(List<Watch> watches)
        {
            using var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, watches);
            memoryStream.Position = 0;
            var fileContent = new StreamContent(memoryStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };
            var content = new MultipartFormDataContent { { fileContent, "file", "MyWatches.json" } };
            using var response = await _httpClient.PostAsync(BaseUrl, content);
        }
    }
}
