using System.IO;
using System.Net;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FunctionPlugin
{
    public class Function1
    {
        private readonly ILogger _logger;
        OpenAIClient client;// = new(azureOpenAIResourceUri, azureOpenAIApiKey);

        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
            Uri azureOpenAIResourceUri = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_ENDPOINT"));
            AzureKeyCredential azureOpenAIApiKey = new(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));
            client = new(azureOpenAIResourceUri, azureOpenAIApiKey);
        }

        [Function("PiratePlugin")]
        public async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            FunctionContext functionContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting streaming response");

            var httpContext = functionContext.GetHttpContext() ?? throw new InvalidOperationException("HttpContext is null");

            httpContext.Response.Headers.ContentType = "text/event-stream";
            var data = await req.ReadFromJsonAsync<Data>();

            var streamWriter = new StreamWriter(httpContext.Response.BodyWriter.AsStream());

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

            var responseStream = await client.GetChatCompletionsStreamingAsync(chatCompletionsOptions);
            string responseId = Guid.NewGuid().ToString();
            await foreach (var response in responseStream)
            {
                responseId = response.Id;
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

                if (response.FinishReason.HasValue && response.FinishReason.Value == CompletionsFinishReason.Stopped)
                {
                    await WriteDebugMessageAsync(responseId, streamWriter);
                }

                string dataLine = $"data: {JsonSerializer.Serialize(completionChunk)}";
                await streamWriter.WriteLineAsync(dataLine);
                await streamWriter.WriteLineAsync();
                await streamWriter.FlushAsync();
            }

            // write done line
            await streamWriter.WriteLineAsync("data: [DONE]");
            await streamWriter.WriteLineAsync();
            await streamWriter.FlushAsync();

            _logger.LogInformation("Streaming done.");
        }

        public async Task WriteDebugMessageAsync(string responseId, StreamWriter streamWriter)
        {
            // create the expected OAI chunk format
            CompletionChunk completionChunk = new()
            {
                Model = "gpt-3.5-turbo",
                Id = responseId,
                Choices =
                [
                    new()
                        {
                            Delta = new() { Content = "DEBUG: This comes from Azure Functions" },
                            FinishReason = null
                        }
                ]
            };

            string dataLine = $"data: {JsonSerializer.Serialize(completionChunk)}";
            await streamWriter.WriteLineAsync(dataLine);
            await streamWriter.WriteLineAsync();
            await streamWriter.FlushAsync();
        }
    }
}
