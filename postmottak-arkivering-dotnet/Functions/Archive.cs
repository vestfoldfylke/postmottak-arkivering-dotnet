using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using postmottak_arkivering_dotnet.Contracts;

namespace postmottak_arkivering_dotnet.Functions;

public class Archive
{
    private readonly ILogger<Archive> _logger;

    public Archive(ILogger<Archive> logger)
    {
        _logger = logger;
    }

    [Function("ArchiveEmails")]
    [OpenApiOperation(operationId: "SendMail")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Executed successfully")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "application/json", typeof(ErrorResponse), Description = "Error occured")]
    public IActionResult ArchiveEmails([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}