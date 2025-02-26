using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

Result<double> Parse(string input) =>
    Result.Try(() => double.Parse(input));

Result<double> Divide(double x, double y) =>
    Result.Try(() => x / y);

async Result<double> Do(string a, string b)
{
    var x = await Parse(a);
    var y = await Parse(b);
    Console.WriteLine("Successfully parsed inputs");
    return await Divide(x, y);
}

// Usage
Console.WriteLine(Do("a", "b"));  // Will display the error from Parse("a")


// Implementation
[AsyncMethodBuilder(typeof(ResultAsyncMethodBuilder<>))]
public struct Result<T>
{
    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(Exception error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Exception? Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Fail(Exception error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public ResultAwaiter<T> GetAwaiter() => new ResultAwaiter<T>(this);

    public override string ToString() => IsSuccess ? Value?.ToString() : $"Error: {Error?.Message}";
}

public static class Result
{
    public static Result<T> Try<T>(Func<T> function)
    {
        try
        {
            return Result<T>.Success(function());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex);
        }
    }
}

public struct ResultAwaiter<T> : ICriticalNotifyCompletion
{
    private readonly Result<T> _result;

    public ResultAwaiter(Result<T> result)
    {
        _result = result;
    }

    public bool IsCompleted => true;

    public T GetResult()
    {
        if (!_result.IsSuccess)
            ResultAsyncMethodBuilder<T>.SetError(_result.Error);

        return _result.IsSuccess ? _result.Value : default;
    }

    public void OnCompleted(Action continuation) => continuation();
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

public struct ResultAsyncMethodBuilder<T>
{
    private Result<T> _result;
    private Exception _exception;

    public static ResultAsyncMethodBuilder<T> Create() =>
        new ResultAsyncMethodBuilder<T>();

    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine 
        => stateMachine.MoveNext();

    public void SetResult(T result)
        => _result = Result<T>.Success(result);

    public void SetException(Exception exception)
    {
        _exception = exception;
        _result = Result<T>.Fail(exception);
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        var completionAction = CreateCompletionAction(ref stateMachine);
        awaiter.OnCompleted(completionAction);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        var completionAction = CreateCompletionAction(ref stateMachine);
        awaiter.UnsafeOnCompleted(completionAction);
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine) { }

    public static void SetError(Exception exception)
        => throw new ResultException(exception);

    public Result<T> Task
    {
        get
        {
            if (_exception is ResultException resultException)
                return Result<T>.Fail(resultException.InnerException);

            return _result;
        }
    }

    private Action CreateCompletionAction<TStateMachine>(
        ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
    {
        var boxedStateMachine = stateMachine;
        return boxedStateMachine.MoveNext;
    }

    private class ResultException : Exception
    {
        public ResultException(Exception innerException)
            : base("Result operation failed", innerException) { }
    }
}