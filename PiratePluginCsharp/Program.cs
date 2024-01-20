using Azure;
using Azure.AI.OpenAI;
using PiratePluginCsharp;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

#region throwaway callback method
app.Map("/oauth/callback", () =>
{
    Debug.WriteLine("OAuth callback");
    return Results.Ok();
})
.WithName("oauthcallback")
.WithOpenApi();
#endregion

Uri azureOpenAIResourceUri = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_ENDPOINT"));
AzureKeyCredential azureOpenAIApiKey = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));
OpenAIClient client = new(azureOpenAIResourceUri, azureOpenAIApiKey);

app.MapPost("/arrrr", async (Data data) =>
{
    var chatCompletionsOptions = new ChatCompletionsOptions()
    {
        DeploymentName = "chat", // Use DeploymentName for "model" with non-Azure clients
        Messages =
            {
                // The system message represents instructions or other guidance about how the assistant should behave
                new ChatRequestSystemMessage("You are a helpful assistant. You will give all responses as if you were a pirate giving pirate advice. You will talk like a pirate."),
                // User messages represent current or historical input from the end user
                new ChatRequestUserMessage("Can you help me?"),
                // Assistant messages represent historical responses from the assistant
                new ChatRequestAssistantMessage("Arrrr! Of course, me hearty! What can I do for ye?"),
                new ChatRequestUserMessage(data.messages?.LastOrDefault(m=>m.Role == "user")?.Content),
            }
    };

    // stream the response
    async Task StreamContentUpdatesAsync(Stream stream)
    {
        TextWriter textWriter = new StreamWriter(stream);
        var responseStream = await client.GetChatCompletionsStreamingAsync(chatCompletionsOptions);
        await foreach (var response in responseStream)
        {
            // create the expected OAI chunk format
            CompletionChunk completionChunk = new()
            {
                Model = "gpt-3.5-turbo",
                Id = response.Id,
                Choices =
                [
                    new()
                        {
                            Delta = new() { Content = response.ContentUpdate },
                            FinishReason = response.FinishReason.HasValue ? response.FinishReason.Value.ToString() : null
                        }
                ]
            };

            string dataLine = $"data: {JsonSerializer.Serialize(completionChunk)}";
            await textWriter.WriteLineAsync(dataLine);
            await textWriter.WriteLineAsync(string.Empty);
            await textWriter.FlushAsync();
        }

        // write done line
        await textWriter.WriteLineAsync("data: [DONE]");
        await textWriter.WriteLineAsync(string.Empty);
        await textWriter.FlushAsync();
    }

    return Results.Stream(StreamContentUpdatesAsync, "text/event-stream");
})
.WithName("arrrr")
.WithOpenApi();

app.UseHttpsRedirection();

app.Run();