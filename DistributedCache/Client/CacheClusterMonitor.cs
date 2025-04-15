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
    
    public async Task<T> GetAllNodeAsync<T>()
    {
        var response = await _httpClient.GetAsync(_clusterManagerUrl + $"api/cluster/cache/all-nodes");
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