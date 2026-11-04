using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace nesimokau.lt.Services;

/// <summary>Thin client for the same-site /api backend. Auth is an HttpOnly cookie, so no tokens live in JS.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(12);
    private readonly HttpClient http = new();
    private readonly string baseUrl;

    public ApiClient(IConfiguration config, Microsoft.AspNetCore.Components.NavigationManager navigation)
    {
        var configured = config["Api:BaseUrl"];
        baseUrl = string.IsNullOrWhiteSpace(configured) ? navigation.BaseUri.TrimEnd('/') : configured.TrimEnd('/');
    }

    public sealed record ApiResponse(HttpStatusCode Status, JsonElement? Body, string? Error)
    {
        public bool Ok => (int)Status is >= 200 and < 300;

        public T? As<T>() => Body is { ValueKind: not JsonValueKind.Null } b ? b.Deserialize<T>(JsonOptions) : default;

        public string? GetString(string name)
            => Body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        public bool GetBool(string name)
            => Body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    }

    public Task<ApiResponse> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);
    public Task<ApiResponse> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body ?? new { });
    public Task<ApiResponse> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    private async Task<ApiResponse> SendAsync(HttpMethod method, string path, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{baseUrl}/api{path}");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            request.Headers.TryAddWithoutValidation("X-Requested-With", "nesimokau");
            if (body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            }

            using var cts = new CancellationTokenSource(Timeout);
            using var response = await http.SendAsync(request, cts.Token);
            var text = await response.Content.ReadAsStringAsync(cts.Token);

            JsonElement? json = null;
            string? error = null;
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                try
                {
                    json = JsonDocument.Parse(text).RootElement.Clone();
                    if (json.Value.ValueKind == JsonValueKind.Object && json.Value.TryGetProperty("error", out var e)) error = e.GetString();
                }
                catch (JsonException)
                {
                    // non-JSON body (e.g. proxy error page)
                }
            }

            return new ApiResponse(response.StatusCode, json, error);
        }
        catch (OperationCanceledException)
        {
            return new ApiResponse(HttpStatusCode.RequestTimeout, null, "Serveris neatsako laiku. Pabandyk dar kartą.");
        }
        catch (HttpRequestException)
        {
            return new ApiResponse(HttpStatusCode.ServiceUnavailable, null, "Nepavyko prisijungti prie serverio. Patikrink internetą.");
        }
    }
}
