using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using postmottak_arkivering_dotnet.Middleware;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using postmottak_arkivering_dotnet.Utils;
using VFK.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.UseMiddleware<ErrorHandlingMiddleware>();

builder.Logging.AddVfkLogging();

builder.Services.AddSingleton<IEmailTypeService, EmailTypeService>();
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IGraphService, GraphService>();
builder.Services.AddSingleton<IArchiveService, ArchiveService>();
builder.Services.AddSingleton<IBlobService, BlobService>();

// AI Agent Services
builder.Services.AddSingleton<IAiPengetransportenService, AiPengetransportenService>();
builder.Services.AddSingleton<IAiPluginTestService, AiPluginTestService>();
builder.Services.AddSingleton<IAiRf1350Service, AiRf1350Service>();

AiHelper.ConfigurationManager = builder.Configuration;

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Build().Run();