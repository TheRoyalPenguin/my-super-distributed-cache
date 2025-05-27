using ClusterManager.Common;
using ClusterManager.Enums;
using ClusterManager.Interfaces;

namespace ClusterManager.Services;

public class HttpService(HttpClient _httpClient): IHttpService
{
    public async Task<Result<HttpResponseMessage>> SendRequestAsync<T>(string url, string endpoint, HttpMethodEnum method, T? item = default)
    {
        string baseUrl = url.EndsWith("/") ? url : url + "/";
        var requestUri = new Uri(baseUrl + endpoint);
        HttpResponseMessage response;

        switch (method)
        {
            case HttpMethodEnum.Get:
                response = await _httpClient.GetAsync(requestUri);
                break;
            case HttpMethodEnum.Put:
                response = await _httpClient.PutAsJsonAsync(requestUri, item);
                break;
            case HttpMethodEnum.Post:
                response = await _httpClient.PostAsJsonAsync(requestUri, item);
                break;
            case HttpMethodEnum.Delete:
                response = await _httpClient.DeleteAsync(requestUri);
                break;
            default:
                throw new NotSupportedException("Метод " + method.ToString() + " не поддерживается.");
        }

        if (response == null)
        {
            return Result<HttpResponseMessage>.Fail("Нет ответа от сервера.", 500);
        }
        if (!response.IsSuccessStatusCode)
        {
            return Result<HttpResponseMessage>.Fail(response.Content != null ? await response.Content.ReadAsStringAsync() : "", (int)response.StatusCode);
        }

        return Result<HttpResponseMessage>.Ok(response, 200);
    }
}
