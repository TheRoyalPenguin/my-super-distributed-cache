using System.Net.Http.Json;

namespace Client;

public class CacheClusterMonitor
{
    private readonly HttpClient _httpClient;
    private readonly Uri _clusterManagerUrl;
    
    public CacheClusterMonitor(HttpClient httpClient, Uri clusterManagerUrl)
    {
        _httpClient = httpClient;
        _clusterManagerUrl = new Uri(clusterManagerUrl.ToString().EndsWith("/") ? clusterManagerUrl.ToString() : clusterManagerUrl.ToString() + "/");
    }
    
    public async Task<T> GetAllNodesWithDataAsync<T>()
    {
        var response = await _httpClient.GetAsync(_clusterManagerUrl + $"api/monitor/nodes");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        else
        {
            throw new Exception();
        }
    }
    
    public async Task<T> GetNodeWithDataAsync<T>(string key)
    {
        var response = await _httpClient.GetAsync(_clusterManagerUrl + $"api/monitor/node/" + Uri.EscapeDataString(key));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        else
        {
            throw new Exception();
        }
    }
    
    public async Task<T> GetStatusNodesAsync<T>()
    {
        var response = await _httpClient.GetAsync(_clusterManagerUrl + $"api/monitor/nodes-status");
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        else
        {
            throw new Exception();
        }
    }
}