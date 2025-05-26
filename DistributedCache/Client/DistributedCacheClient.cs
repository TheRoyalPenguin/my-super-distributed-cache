using Node.DTO;
using System.Net.Http.Json;

namespace Client;

public class DistributedCacheClient
{
   private readonly HttpClient _httpClient;
    private readonly Uri _primaryManagerUrl;
    private readonly Uri _backupManagerUrl;

    public DistributedCacheClient(HttpClient httpClient, Uri primaryManagerUrl, Uri backupManagerUrl)
    {
        _httpClient = httpClient;
        _primaryManagerUrl = new Uri(primaryManagerUrl.ToString().TrimEnd('/') + "/");
        _backupManagerUrl = new Uri(backupManagerUrl.ToString().TrimEnd('/') + "/");
    }

    private async Task<HttpResponseMessage> TryWithFailover(Func<Uri, Task<HttpResponseMessage>> request)
    {
        try
        {
            var response = await request(_primaryManagerUrl);
            return response; 
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

    public async Task<T> GetAsync<T>(string itemKey)
    {
        var response = await TryWithFailover(url =>
            _httpClient.GetAsync(url + $"api/cluster/cache/" + Uri.EscapeDataString(itemKey)));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>();
            if (result is null)
                throw new Exception("Response deserialization failed.");

            return result;
        }
        else
        {
            throw new Exception($"Cache GET failed with status code {response.StatusCode}");
        }
    }

    public async Task SetCacheAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var dto = new CacheItemDto
        {
            Key = key,
            Value = value,
            TTL = ttl
        };

        var response = await TryWithFailover(url =>
            _httpClient.PutAsJsonAsync(url + "api/cluster/cache/", dto));
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Cache SET failed with status code {response.StatusCode}");
        }
    }

    public async Task SetAsync<T>(string key, T value, int? ttlSeconds = null)
    {
        await SetCacheAsync(key, value, ttlSeconds.HasValue ? TimeSpan.FromSeconds(ttlSeconds.Value) : null);
    }
    public async Task<string> CreateNodeAsync(string containerName, int copiesCount = 1)
    {
        if (copiesCount < 1 || copiesCount > 10)
            throw new ArgumentOutOfRangeException(nameof(copiesCount), "Количество копий должно быть от 1 до 10");

        var response = await TryWithFailover(url =>
            _httpClient.PostAsync(url + $"api/cluster/nodes/create/{Uri.EscapeDataString(containerName)}/{copiesCount}", null));

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Node creation failed with status code {response.StatusCode}");

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> DeleteNodeAsync(string containerName, bool force = false)
    {
        var response = await TryWithFailover(url =>
            _httpClient.DeleteAsync(url + $"api/cluster/nodes/delete/{Uri.EscapeDataString(containerName)}?force={force.ToString().ToLower()}"));

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Node deletion failed with status code {response.StatusCode}");

        return await response.Content.ReadAsStringAsync();
    }
}
