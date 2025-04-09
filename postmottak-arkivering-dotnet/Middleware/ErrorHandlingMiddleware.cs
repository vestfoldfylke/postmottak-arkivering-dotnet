using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using postmottak_arkivering_dotnet.Contracts;

namespace postmottak_arkivering_dotnet.Middleware;

public class ErrorHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger)
    {
        _logger = logger;
    }
    
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing the request.");
            
            var request = await context.GetHttpRequestDataAsync();
            var response = request!.CreateResponse();
            response.StatusCode = System.Net.HttpStatusCode.InternalServerError;

            var errorMessage = new ErrorResponse("An unhandled exception occured", ex.Message);
            var jsonResponse = JsonSerializer.Serialize(errorMessage);
            
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(jsonResponse);
            
            context.GetInvocationResult().Value = response;
        }
    }
}