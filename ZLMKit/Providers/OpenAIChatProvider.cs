/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZLMKit.LMChat;
using ZLMKit.Protocols;

namespace ZLMKit.Providers;

public class OpenAIChatProvider : ILMChatProvider
{
    private readonly ILMChat fChat;
    private readonly HttpClient fHttpClient;
    private readonly CancellationTokenSource fTokenSource;
    private List<ToolDef> fTools;

    public OpenAIChatProvider(ILMChat chat)
    {
        fChat = chat;

        fHttpClient = new HttpClient();
        fHttpClient.Timeout = Timeout.InfiniteTimeSpan;
        fHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        fHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        fTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Cancel current request.
    /// </summary>
    public void CancelRequest()
    {
        fTokenSource.Cancel();
        fTokenSource.TryReset();
    }

    private void RequireHttp()
    {
        var settings = fChat.Settings;

        if (fHttpClient.BaseAddress == null)
            fHttpClient.BaseAddress = new Uri(settings.APIAddress);

        if (!string.IsNullOrEmpty(settings.APIKey))
            fHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.APIKey);
    }

    public async Task<List<ModelData>> LoadModelsAsync()
    {
        RequireHttp();

        var response = await fHttpClient.GetAsync("v1/models");
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        return data.Data;
    }

    /// <summary>
    /// Single request (waiting for the model's complete response).
    /// </summary>
    public async Task<string> SendMessageSingleAsync(string role, string content, float temperature)
    {
        RequireHttp();

        var messages = new List<ChatMessage>();
        messages.Add(new ChatMessage(role, content));

        var settings = fChat.Settings;

        var request = new ChatRequest(settings.ModelId, messages, false);
        request.Temperature = temperature;
        request.TopP = settings.TopP;
        request.PresencePenalty = settings.PresencePenalty;
        request.FrequencyPenalty = settings.FrequencyPenalty;
        request.MaxTokens = settings.MaxTokens;

        var response = await fHttpClient.PostAsJsonAsync("v1/chat/completions", request, fTokenSource.Token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(fTokenSource.Token);
        var message = result?.Choices?.FirstOrDefault()?.Message;
        if (message != null && message.Content != null) {
            // Handle both string and array content
            if (message.Content is string contentString) {
                if (!string.IsNullOrEmpty(contentString)) {
                    return contentString;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Request.
    /// </summary>
    public async Task SendMessageAsync()
    {
        RequireHttp();

        var settings = fChat.Settings;
        fChat.RequestId += 1;
        fTokenSource.TryReset();

        var request = CreateRequest(settings.StreamMode);

        var response = await fHttpClient.PostAsJsonAsync("v1/chat/completions", request, fTokenSource.Token);
        response.EnsureSuccessStatusCode();

        if (!settings.StreamMode) {
            // Regular request (waiting for the model's complete response).

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>(fTokenSource.Token);
            var message = result?.Choices?.FirstOrDefault()?.Message;
            if (message != null)
                await ProcessReceivedMessage(message);
        } else {
            // Streaming request (streaming tokens as they are generated).

            string fullResponse = "";
            var accumulatedToolCalls = new Dictionary<int, ToolCall>();

            try {
                fChat.View?.StartStreamingMessage(fChat.RequestId);

                var stream = await response.Content.ReadAsStreamAsync(fTokenSource.Token);
                var tokenStream = StreamTokensAndToolCallsAsync(stream, accumulatedToolCalls, fTokenSource.Token);
                await foreach (var item in tokenStream) {
                    if (item.IsToken) {
                        fullResponse += item.Content;
                        fChat.View?.UpdateStreamingMessage(fChat.RequestId, fullResponse);
                    } else if (item.IsToolCall) {
                        // Tool calls are being accumulated in accumulatedToolCalls list
                        // We could display tool calls as they arrive if needed
                    }
                }
            } finally {
                // Add the complete assistant message to history
                await fChat.AddHistory("assistant", fullResponse);
                fChat.View?.FinalizeStreamingMessage(fChat.RequestId);

                // Process tool calls if any were received
                if (accumulatedToolCalls.Count > 0) {
                    // Create a message containing the tool calls
                    var toolCallMessage = new ChatMessage("assistant", "");
                    toolCallMessage.ToolCalls = accumulatedToolCalls.Values.ToList();
                    await ProcessReceivedMessage(toolCallMessage);
                }
            }
        }
    }

    private async Task ProcessReceivedMessage(ChatMessage message)
    {
        // Add assistant's message to history (null if tool_calls are present)
        if (message.Content != null) {
            // Handle both string and array content
            if (message.Content is string contentString) {
                if (!string.IsNullOrEmpty(contentString)) {
                    await fChat.ProcessMessage(message);
                }
            } else {
                // For array content, we still add the message to history but don't display it
                await fChat.ProcessMessage(message);
            }
        }

        // Check for tool calls
        if (message.ToolCalls != null && message.ToolCalls.Count > 0) {
            // Process tool calls
            await ProcessToolCallsAsync(message.ToolCalls);
        }
    }

    /// <summary>
    /// Process tool calls and return result
    /// </summary>
    private async Task ProcessToolCallsAsync(List<ToolCall> toolCalls)
    {
        if (fChat.MCPServer == null)
            return;

        fTokenSource.TryReset();

        // Create new message for tool results
        var toolResults = new List<ChatMessage>();

        foreach (var toolCall in toolCalls) {
            if (toolCall.Type != "function" || toolCall.Function == null) continue;
            string funcName = toolCall.Function.Name;

            ChatMessage toolResult = null;
            try {
                fChat.View?.ShowMessage($"Attempt to call the tool '{funcName}'", "system");

                // Parse tool arguments
                JsonElement args = JsonDocument.Parse(toolCall.Function.Arguments).RootElement;

                // Execute tool via MCPController
                var contents = fChat.MCPServer.ExecuteTool(funcName, args);

                // Convert result to string
                string resultText = string.Join("\n", contents.Select(c => c.Text ?? ""));

                // Create message with tool result
                toolResult = new ChatMessage("tool", resultText);
            } catch (Exception ex) {
                // In case of error, create message with error description
                toolResult = new ChatMessage("tool", $"Error executing tool '{funcName}': {ex.Message}");
            }

            if (toolResult != null) {
                toolResults.Add(toolResult);
                // Add tool results to history
                await fChat.ProcessMessage(toolResult);
            }
        }

        // If tools were called, send a new request with results
        if (toolResults.Count > 0) {
            fTokenSource.TryReset();

            // Create new request with tool results
            var request = CreateRequest(false);
            var response = await fHttpClient.PostAsJsonAsync("v1/chat/completions", request, fTokenSource.Token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>(fTokenSource.Token);
            var finalMessage = result?.Choices?.FirstOrDefault()?.Message;
            // Add final assistant message to history
            await fChat.ProcessMessage(finalMessage);
        }
    }

    private ChatRequest CreateRequest(bool stream)
    {
        var settings = fChat.Settings;
        var request = new ChatRequest(settings.ModelId, fChat.HistoryStorage.CurrentHistory, stream);
        request.Temperature = settings.Temperature;
        request.TopP = settings.TopP;
        request.PresencePenalty = settings.PresencePenalty;
        request.FrequencyPenalty = settings.FrequencyPenalty;
        request.MaxTokens = settings.MaxTokens;

        if (fChat.MCPServer != null) {
            var mcpTools = fChat.MCPServer.MCPTools;
            if (fTools == null || fTools.Count != mcpTools.Count) {
                fTools = mcpTools.Select(mT => new ToolDef(mT)).ToList();
            }
            request.Tools = fTools;
        }

        return request;
    }

    private static async IAsyncEnumerable<StreamItem> StreamTokensAndToolCallsAsync(
        Stream stream,
        Dictionary<int, ToolCall> accumulatedToolCalls,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream) {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var jsonText = line["data: ".Length..].Trim();
            if (jsonText == "[DONE]")
                break;

            ChatStreamResponse chunk = null;
            try {
                chunk = JsonSerializer.Deserialize<ChatStreamResponse>(jsonText);
            } catch { /* Ignore incomplete lines */ }

            if (chunk?.Choices?.FirstOrDefault() is { } choice) {
                // Handle content tokens
                var content = choice.Delta?.Content;
                if (!string.IsNullOrEmpty(content)) {
                    yield return new StreamItem { IsToken = true, Content = content };
                }

                // Handle tool calls
                if (choice.Delta?.ToolCalls != null) {
                    foreach (var toolCallDelta in choice.Delta.ToolCalls) {
                        // Find or create the tool call in our accumulated list
                        ToolCall existingToolCall;
                        if (!accumulatedToolCalls.TryGetValue(toolCallDelta.Index, out existingToolCall)) {
                            existingToolCall = new ToolCall();
                            accumulatedToolCalls[toolCallDelta.Index] = existingToolCall;
                        }

                        // Update the tool call with the delta information
                        if (!string.IsNullOrEmpty(toolCallDelta.Id)) {
                            existingToolCall.Id = toolCallDelta.Id;
                        }
                        if (!string.IsNullOrEmpty(toolCallDelta.Type)) {
                            existingToolCall.Type = toolCallDelta.Type;
                        }
                        if (toolCallDelta.Function != null) {
                            if (existingToolCall.Function == null) {
                                existingToolCall.Function = new FunctionCall();
                            }
                            if (!string.IsNullOrEmpty(toolCallDelta.Function.Name)) {
                                existingToolCall.Function.Name = toolCallDelta.Function.Name;
                            }
                            if (!string.IsNullOrEmpty(toolCallDelta.Function.Arguments)) {
                                existingToolCall.Function.Arguments += toolCallDelta.Function.Arguments;
                            }
                        }

                        yield return new StreamItem { IsToolCall = true, ToolCall = existingToolCall };
                    }
                }
            }
        }
    }

    private class StreamItem
    {
        public bool IsToken { get; set; }
        public string Content { get; set; }
        public bool IsToolCall { get; set; }
        public ToolCall ToolCall { get; set; }
    }
}
