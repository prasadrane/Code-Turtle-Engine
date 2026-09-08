using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace CodeTurtleEngine.Llm;

public interface IChatClientFactory
{
    IChatClient Create(RouteOptions route, string model);
}

public sealed class BailianChatClientFactory : IChatClientFactory
{
    public IChatClient Create(RouteOptions route, string model)
    {
        var baseUrl = Environment.GetEnvironmentVariable(route.BaseUrlEnv)
            ?? throw new ProviderException($"Missing env '{route.BaseUrlEnv}'.", Array.Empty<string>());
        var apiKey = Environment.GetEnvironmentVariable(route.ApiKeyEnv)
            ?? throw new ProviderException($"Missing env '{route.ApiKeyEnv}'.", Array.Empty<string>());

        var openAi = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        // Microsoft.Extensions.AI.OpenAI 10.9.0: OpenAIChatClient is internal;
        // the public adapter is the AsIChatClient() extension on OpenAI.Chat.ChatClient.
        return openAi.GetChatClient(model).AsIChatClient();
    }
}
