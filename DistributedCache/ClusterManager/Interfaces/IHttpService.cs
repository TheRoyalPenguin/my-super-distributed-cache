namespace ClusterManager.Interfaces;

public interface IHttpService
{
    Task<Result<HttpResponseMessage>> SendRequestAsync<T>(string url, string endpoint, HttpMethodEnum method, T? item = default);
}
