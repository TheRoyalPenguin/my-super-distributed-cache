namespace ClusterManager;

public class Result<T>
{
    public T? Data { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    public Result(T? data, string? error, bool isSuccess)
    {
        Data = data;
        Error = error;
        IsSuccess = isSuccess;
    }

    public static Result<T> Ok(T data)
    {
        return new Result<T>(data, null, true);
    }
    public static Result<T> Fail(string error)
    {
        return new Result<T>(default, error, false);
    }
}
