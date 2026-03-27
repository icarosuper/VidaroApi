using MediatR;
using Microsoft.Extensions.Logging;

namespace VidroApi.Application.Behaviors;

public sealed class RequestLoggingPipelineBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var featureName = typeof(TRequest).DeclaringType?.Name ?? typeof(TRequest).Name;

        logger.LogInformation("[{FeatureName}] Handling request.", featureName);

        try
        {
            var response = await next();

            // Result<T, Error> and UnitResult<Error> from CSharpFunctionalExtensions don't share a
            // common interface, so we inspect IsFailure via reflection to keep this behavior generic.
            var isFailure = typeof(TResponse).GetProperty("IsFailure")?.GetValue(response) as bool?;

            if (isFailure is true)
            {
                var error = typeof(TResponse).GetProperty("Error")?.GetValue(response);
                logger.LogWarning("[{FeatureName}] Request completed with error. {@Error}", featureName, error);
            }
            else
            {
                logger.LogInformation("[{FeatureName}] Request completed successfully.", featureName);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{FeatureName}] Request failed with unhandled exception.", featureName);
            throw;
        }
    }
}
