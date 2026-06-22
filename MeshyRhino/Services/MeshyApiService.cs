// <author>QROST</author>

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MeshyRhino.Models;
using Newtonsoft.Json;

namespace MeshyRhino.Services
{
    /// <summary>
    /// Thrown when the Meshy API returns a non-success HTTP status code.
    /// Carries the status code so callers can distinguish transient failures
    /// (worth retrying) from permanent ones (fail fast).
    /// </summary>
    public class MeshyApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public MeshyApiException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// 5xx server errors and 429 (Too Many Requests) are transient and
        /// safe to retry; 4xx client errors (bad request, invalid key,
        /// insufficient credits, not found) are not.
        /// </summary>
        public bool IsTransient =>
            (int)StatusCode >= 500 || StatusCode == (HttpStatusCode)429;
    }

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

        #region Retexture

        public async Task<string> CreateRetextureAsync(RetextureRequest request, CancellationToken ct = default)
        {
            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/openapi/v1/retexture", content, ct);
            await EnsureSuccessAsync(response);

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<MeshyTaskCreateResponse>(body);
            return result.Result;
        }

        public async Task<MeshyTaskStatus> GetRetextureTaskAsync(string taskId, CancellationToken ct = default)
        {
            var response = await _httpClient.GetAsync($"/openapi/v1/retexture/{taskId}", ct);
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
            int pollIntervalMs = 3000,
            int maxTransientRetries = 2)
        {
            int consecutiveFailures = 0;

            while (!ct.IsCancellationRequested)
            {
                MeshyTaskStatus status;
                try
                {
                    status = await GetTaskStatusAsync(taskId, mode, ct);
                    consecutiveFailures = 0;
                }
                catch (Exception ex) when (
                    IsTransientFailure(ex) &&
                    !ct.IsCancellationRequested &&
                    consecutiveFailures < maxTransientRetries)
                {
                    // A transient blip while polling should not abort a job that
                    // may already be most of the way through generation.
                    consecutiveFailures++;
                    await Task.Delay(pollIntervalMs, ct);
                    continue;
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

        private Task<MeshyTaskStatus> GetTaskStatusAsync(string taskId, GenerationMode mode, CancellationToken ct)
        {
            switch (mode)
            {
                case GenerationMode.TextTo3D:
                    return GetTextTo3DTaskAsync(taskId, ct);
                case GenerationMode.ImageTo3D:
                    return GetImageTo3DTaskAsync(taskId, ct);
                case GenerationMode.MultiImageTo3D:
                    return GetMultiImageTo3DTaskAsync(taskId, ct);
                case GenerationMode.Retexture:
                    return GetRetextureTaskAsync(taskId, ct);
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
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

        public async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries, CancellationToken ct)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (
                    attempt < maxRetries &&
                    !ct.IsCancellationRequested &&
                    IsTransientFailure(ex))
                {
                    attempt++;
                    int delayMs = 1000 * (1 << attempt);
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        /// <summary>
        /// Determines whether a failure is worth retrying: raw network errors,
        /// request timeouts (surfaced by HttpClient as <see cref="TaskCanceledException"/>),
        /// and transient HTTP status codes (5xx / 429). Permanent client errors
        /// (4xx) and user cancellations are not treated as transient.
        /// </summary>
        private static bool IsTransientFailure(Exception ex)
        {
            switch (ex)
            {
                case MeshyApiException me:
                    return me.IsTransient;
                case HttpRequestException _:
                    return true;
                case TaskCanceledException _:
                    // A genuine user cancellation is filtered out by the caller's
                    // CancellationToken check before this predicate is evaluated,
                    // so reaching here means the request timed out.
                    return true;
                default:
                    return false;
            }
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new MeshyApiException(
                    response.StatusCode,
                    $"[Meshy Rhino] API error ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
