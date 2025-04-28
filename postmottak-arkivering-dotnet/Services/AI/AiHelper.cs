using System;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;

namespace postmottak_arkivering_dotnet.Services.AI;

public static class AiHelper
{
    public static T? GetLatestAnswer<T>(ChatHistory chatHistory)
    {
        // 0 will be the user input. 1 will be the AI response, and so on...
        if (chatHistory.Count < 2)
        {
            return default;
        }
        
        var content = chatHistory[^1].Content;
        if (string.IsNullOrEmpty(content))
        {
            return default;
        }
        
        return JsonSerializer.Deserialize<T>(content) ?? throw new InvalidOperationException($"Failed to deserialize AI response into type {typeof(T).Name}");
    }
}