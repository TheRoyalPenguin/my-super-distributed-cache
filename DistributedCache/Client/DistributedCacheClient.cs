using Node.DTO;
using System.Net.Http.Json;

namespace Client;

public class DistributedCacheClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _clusterManagerUrl;

    public DistributedCacheClient(HttpClient httpClient, Uri clusterManagerUrl)
    {
        _httpClient = httpClient;
        _clusterManagerUrl = new Uri(clusterManagerUrl.ToString().EndsWith("/") ? clusterManagerUrl.ToString() : clusterManagerUrl.ToString() + "/");
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var response = await _httpClient.GetAsync(_clusterManagerUrl + $"api/cluster/cache/" + Uri.EscapeDataString(key));
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        else
        {
            throw new Exception();
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var dto = new CacheItemDto
        {
            Value = value,
            TTL = ttl
        };

        var response = await _httpClient.PutAsJsonAsync(_clusterManagerUrl + "api/cluster/cache/" + Uri.EscapeDataString(key), dto);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception();
        }
    }
}
