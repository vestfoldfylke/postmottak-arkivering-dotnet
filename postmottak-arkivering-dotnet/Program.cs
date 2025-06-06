/*using System;
using System.Diagnostics;*/
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using postmottak_arkivering_dotnet.Middleware;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using postmottak_arkivering_dotnet.Utils;
using Prometheus;
using Vestfold.Extensions.Archive;
using Vestfold.Extensions.Archive.Services;
using Vestfold.Extensions.Authentication;
using Vestfold.Extensions.Authentication.Services;
using Vestfold.Extensions.Logging;
using Vestfold.Extensions.Metrics;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ErrorHandlingMiddleware>();

builder.Logging.AddVestfoldLogging();
builder.Services.AddVestfoldAuthentication();
builder.Services.AddVestfoldArchive();
builder.Services.AddVestfoldMetrics();

/*Serilog.Debugging.SelfLog.Enable(msg =>
{
     Debug.WriteLine($"Æ har dreti i bidet: {msg}");
     Console.WriteLine($"Æ har dreti i bidet: {msg}");
});*/

// Configure the service container to collect Prometheus metrics from all registered HttpClients
builder.Services.UseHttpClientMetrics();

builder.Services.AddSingleton<IEmailTypeService, EmailTypeService>();
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IGraphService, GraphService>();
builder.Services.AddSingleton<IArchiveService, ArchiveService>();
builder.Services.AddSingleton<IBlobService, BlobService>();
builder.Services.AddSingleton<IStatisticsService, StatisticsService>();

// AI Agent Services
builder.Services.AddSingleton<IAiArntIvanService, AiArntIvanService>();
builder.Services.AddSingleton<IAiPluginTestService, AiPluginTestService>();

AiHelper.ConfigurationManager = builder.Configuration;

builder.Services
     .AddApplicationInsightsTelemetryWorkerService()
     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();