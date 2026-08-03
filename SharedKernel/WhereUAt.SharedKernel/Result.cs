namespace WhereUAt.SharedKernel;

public record ApplicationError(string Message);

public class Result<T> : Result
{
    public T? Value { get; private set; }

    private Result(T value, IEnumerable<ApplicationError> errors) : base(errors)
    {
        Value = value;
    }

    private Result(IEnumerable<ApplicationError> errors) : base(errors)
    {
    }

    private Result()
    {
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value, []);
    }

    public new static Result<T> Failure(string description)
    {
        return new Result<T>([new ApplicationError(description)]);
    }

    public new static Result<T> Failure(IEnumerable<string> descriptions)
    {
        return new Result<T>(descriptions.Select(description => new ApplicationError(description)));
    }

    public static Result<T> From(Result result, T value)
    {
        return new Result<T>(value, result.Errors);
    }

    public static Result<T> From(Result result)
    {
        return new Result<T>(result.Errors);
    }

    public static Result<T> Empty()
    {
        return new Result<T>();
    }

    public static implicit operator Result<T>(T value)
    {
        return new Result<T>(value, []);
    }
}

public class Result
{
    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public List<ApplicationError> Errors { get; } = [];

    protected Result(IEnumerable<ApplicationError> errors)
    {
        Errors = [.. errors];
    }

    protected Result()
    {
    }

    public static Result Success()
    {
        return new Result();
    }

    public static Result<T> Success<T>(T data)
    {
        return Result<T>.Success(data);
    }

    public static Result<T> From<T>(Result result, T value)
    {
        return Result<T>.From(result, value);
    }

    public static Result Failure(IEnumerable<string> descriptions)
    {
        return new Result(descriptions.Select(description => new ApplicationError(description)));
    }

    public static Result<T> Failure<T>(string description)
    {
        var result = Result<T>.Empty();
        result.AddError(new ApplicationError(description));
        return result;
    }

    public static Result Failure(string description)
    {
        var result = new Result();
        result.AddError(new ApplicationError(description));
        return result;
    }


    public void AddError(ApplicationError error)
    {
        Errors.Add(error);
    }
}