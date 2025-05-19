
using System.Net.Http.Json;

namespace Client;

public class CacheClusterMonitor
{
    private readonly HttpClient _httpClient;
    private readonly Uri _primaryManagerUrl;
    private readonly Uri _backupManagerUrl;

    public CacheClusterMonitor(HttpClient httpClient, Uri primaryManagerUrl, Uri backupManagerUrl)
    {
        _httpClient = httpClient;
        _primaryManagerUrl = new Uri(primaryManagerUrl.ToString().TrimEnd('/') + "/");
        _backupManagerUrl = new Uri(backupManagerUrl.ToString().TrimEnd('/') + "/");
    }

    private async Task<HttpResponseMessage> TryWithFailover(Func<Uri, Task<HttpResponseMessage>> request)
    {
        try
        {
            return await request(_primaryManagerUrl);
        }
        catch (HttpRequestException)
        {
        }

        try
        {
            return await request(_backupManagerUrl);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception("Both primary and backup cluster managers failed.", ex);
        }
    }

    public async Task<T> GetAllNodesWithDataAsync<T>()
    {
        var response = await TryWithFailover(url =>
            _httpClient.GetAsync(url + "api/monitor/nodes"));

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        throw new Exception($"Request failed with status {response.StatusCode}");
    }

    public async Task<T> GetNodeWithDataAsync<T>(string key)
    {
        var response = await TryWithFailover(url =>
            _httpClient.GetAsync(url + "api/monitor/node/" + Uri.EscapeDataString(key)));

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        throw new Exception($"Request failed with status {response.StatusCode}");
    }

    public async Task<T> GetStatusAllNodesAsync<T>()
    {
        var response = await TryWithFailover(url =>
            _httpClient.GetAsync(url + "api/monitor/nodes/status"));

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        throw new Exception($"Request failed with status {response.StatusCode}");
    }

    public async Task<T> GetStatusNodeAsync<T>(string key)
    {
        var response = await TryWithFailover(url =>
            _httpClient.GetAsync(url + "api/monitor/node/status/" + Uri.EscapeDataString(key)));

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        throw new Exception($"Request failed with status {response.StatusCode}");
    }
}
