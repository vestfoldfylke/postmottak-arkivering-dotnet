using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Identity;
using Microsoft.Graph;

namespace postmottak_arkivering_dotnet.Services;

public interface IAuthenticationService
{
    GraphServiceClient CreateGraphClient();
    Task<AccessToken> GetAccessToken(string[] scopes);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly DefaultAzureCredential _azureCredential;
    
    public AuthenticationService()
    {
        DefaultAzureCredentialOptions defaultAzureOptions = new DefaultAzureCredentialOptions
        {
            Diagnostics =
            {
                IsLoggingEnabled = true,
                IsLoggingContentEnabled = true,
                LoggedHeaderNames = { "x-ms-request-id" },
                LoggedQueryParameters = { "api-version" },
                IsAccountIdentifierLoggingEnabled = true
            }
        };
        
        _azureCredential = new DefaultAzureCredential(defaultAzureOptions);
    }
    
    public GraphServiceClient CreateGraphClient()
    {
        string baseUrl = "https://graph.microsoft.com/v1.0";
        
        string[] scopes =
        [
            "https://graph.microsoft.com/.default"
        ];
        
        return new GraphServiceClient(_azureCredential, scopes, baseUrl);
    }

    public async Task<AccessToken> GetAccessToken(string[] scopes)
    {
        /*using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();*/
        return await _azureCredential.GetTokenAsync(new TokenRequestContext(scopes));
    }
}