// <author>QROST</author>

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MeshyRhino.Models;
using Newtonsoft.Json;

namespace MeshyRhino.Services
{
    public class MeshyApiService : IDisposable
    {
        private const string BaseUrl = "https://api.meshy.ai";
        private readonly HttpClient _httpClient;
        private static readonly HttpClient _downloadClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        public MeshyApiService(string apiKey)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        #region Text-to-3D

        public async Task<string> CreateTextTo3DPreviewAsync(TextTo3DPreviewRequest request, CancellationToken ct = default)
        {
            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/openapi/v2/text-to-3d", content, ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<MeshyTaskCreateResponse>(body);
            return result.Result;
        }

        public async Task<string> CreateTextTo3DRefineAsync(TextTo3DRefineRequest request, CancellationToken ct = default)
        {
            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/openapi/v2/text-to-3d", content, ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<MeshyTaskCreateResponse>(body);
            return result.Result;
        }

        public async Task<MeshyTaskStatus> GetTextTo3DTaskAsync(string taskId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/openapi/v2/text-to-3d/{taskId}", ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MeshyTaskStatus>(body);
        }

        #endregion

        #region Image-to-3D

        public async Task<string> CreateImageTo3DAsync(ImageTo3DRequest request, CancellationToken ct = default)
        {
            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/openapi/v1/image-to-3d", content, ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<MeshyTaskCreateResponse>(body);
            return result.Result;
        }

        public async Task<MeshyTaskStatus> GetImageTo3DTaskAsync(string taskId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/openapi/v1/image-to-3d/{taskId}", ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MeshyTaskStatus>(body);
        }

        #endregion

        #region Multi-Image-to-3D

        public async Task<string> CreateMultiImageTo3DAsync(MultiImageTo3DRequest request, CancellationToken ct = default)
        {
            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/openapi/v1/multi-image-to-3d", content, ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<MeshyTaskCreateResponse>(body);
            return result.Result;
        }

        public async Task<MeshyTaskStatus> GetMultiImageTo3DTaskAsync(string taskId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/openapi/v1/multi-image-to-3d/{taskId}", ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MeshyTaskStatus>(body);
        }

        #endregion

        #region Polling & Download

        public async Task<MeshyTaskStatus> PollUntilCompleteAsync(
            string taskId,
            GenerationMode mode,
            IProgress<int> progress = null,
            CancellationToken ct = default,
            int pollIntervalMs = 3000)
        {
            while (!ct.IsCancellationRequested)
            {
                MeshyTaskStatus status;
                switch (mode)
                {
                    case GenerationMode.TextTo3D:
                        status = await GetTextTo3DTaskAsync(taskId, ct);
                        break;
                    case GenerationMode.ImageTo3D:
                        status = await GetImageTo3DTaskAsync(taskId, ct);
                        break;
                    case GenerationMode.MultiImageTo3D:
                        status = await GetMultiImageTo3DTaskAsync(taskId, ct);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode));
                }

                progress?.Report(status.Progress);

                if (status.Status == "SUCCEEDED")
                    return status;

                if (status.Status == "FAILED")
                    throw new Exception($"[Meshy Rhino] Task failed: {status.TaskError?.Message ?? "Unknown error"}");

                if (status.Status == "CANCELED")
                    throw new OperationCanceledException("[Meshy Rhino] Task was canceled.");

                await Task.Delay(pollIntervalMs, ct);
            }

            throw new OperationCanceledException();
        }

        public async Task<string> DownloadObjAsync(string objUrl, CancellationToken ct = default)
        {
            var response = await _downloadClient.GetAsync(objUrl, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct = default)
        {
            var response = await _downloadClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        #endregion

        private async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"[Meshy Rhino] API error ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
