using Azure;
using Azure.AI.OpenAI;
using PiratePluginCsharp;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.MapPost("/arrrr", async (Data data) =>
{
    Uri azureOpenAIResourceUri = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_ENDPOINT"));
    AzureKeyCredential azureOpenAIApiKey = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));
    OpenAIClient client = new(azureOpenAIResourceUri, azureOpenAIApiKey);

    var chatCompletionsOptions = new ChatCompletionsOptions()
    {
        DeploymentName = "gpt4", // Use DeploymentName for "model" with non-Azure clients
        Messages =
            {
                // The system message represents instructions or other guidance about how the assistant should behave
                new ChatRequestSystemMessage("You are a helpful assistant. You will talk like a pirate."),
                // User messages represent current or historical input from the end user
                new ChatRequestUserMessage("Can you help me?"),
                // Assistant messages represent historical responses from the assistant
                new ChatRequestAssistantMessage("Arrrr! Of course, me hearty! What can I do for ye?"),
                new ChatRequestUserMessage("What's the best way to train a parrot?"),
            }
    };

    // this feels super hack, still doesn't work even
    // feels like i'm using the Azure SDK wrong and that I should be more easily be able to return the streamed responses
    async Task StreamContentUpdatesAsync(Stream stream)
    {
        TextWriter textWriter = new StreamWriter(stream);
        var responseStream = await client.GetChatCompletionsStreamingAsync(chatCompletionsOptions);
        await foreach (var response in responseStream)
        {
            if (!string.IsNullOrEmpty(response.ContentUpdate))
            {
                CompletionChunk completionChunk = new();
                completionChunk.Model = "gpt4";
                completionChunk.Id = response.Id;
                completionChunk.Choices = new CompletionChunk.Choice[1];
                completionChunk.Choices[0] = new CompletionChunk.Choice() { Delta = new() { Content = response.ContentUpdate } };

                string dataLine = $"data: {JsonSerializer.Serialize(completionChunk)}";
                await textWriter.WriteLineAsync(dataLine);
                await textWriter.WriteLineAsync(string.Empty);
                await textWriter.FlushAsync();
            }
        }
        await textWriter.WriteLineAsync($"data: [DONE]");
        await textWriter.FlushAsync();
    }

    return Results.Stream(StreamContentUpdatesAsync, "text/event-stream");


    // stuck here
})
.WithName("arrrr")
.WithOpenApi();

app.UseHttpsRedirection();

app.Run();