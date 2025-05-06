namespace ClusterManager;

public class Result<T>
{
    public T? Data { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess { get; }

    public Result(T? data, string? error, bool isSuccess, int statusCode)
    {
        Data = data;
        Error = error;
        IsSuccess = isSuccess;
        StatusCode = statusCode;
    }

    public static Result<T> Ok(T data, int statusCode)
    {
        return new Result<T>(data, null, true, statusCode);
    }
    public static Result<T> Fail(string error, int statusCode)
    {
        return new Result<T>(default, error, false, statusCode);
    }
}
