using Vogen;
using WhereUAt.SharedKernel;

namespace Application;

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this ValueObjectOrError<T> valueObjectOrError)
    {
        if (!valueObjectOrError.IsSuccess)
            return Result<T>.Failure(valueObjectOrError.Error.ErrorMessage);

        return Result<T>.Success(valueObjectOrError.ValueObject);
    }
}