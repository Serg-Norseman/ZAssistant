/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

#if LLS

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;
using ZLMKit.LMChat;
using ZLMKit.Protocols;

namespace ZLMKit.Providers;

public class LLamaChatProvider : ILMChatProvider
{
    private readonly ILMChat fChat;
    private readonly CancellationTokenSource fTokenSource;
    private LLamaWeights fModelWeights;
    private ModelParams fModelParams;
    private LLamaContext fContext;
    private string fModelPath;

    public LLamaChatProvider(ILMChat chat)
    {
        fChat = chat;
        fTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        fModelPath = chat.Settings.APIAddress;
    }

    /// <summary>
    /// Cancel current request.
    /// </summary>
    public void CancelRequest()
    {
        fTokenSource.Cancel();
        fTokenSource.TryReset();
    }

    private void InitializeModel()
    {
        if (fModelWeights != null && fContext != null)
            return;

        if (string.IsNullOrEmpty(fModelPath) || !File.Exists(fModelPath))
            throw new FileNotFoundException($"Model file not found at path: {fModelPath}");

        try {
            fModelParams = new ModelParams(fModelPath) {
                ContextSize = fChat.Settings.ContextSize,
                GpuLayerCount = 38, // Adjust based on GPU availability (0 for CPU only)
                UseMemoryLock = false,
                UseMemorymap = true
            };

            fModelWeights = LLamaWeights.LoadFromFile(fModelParams);
            fContext = new LLamaContext(fModelWeights, fModelParams);
        } catch (Exception ex) {
            throw new InvalidOperationException($"Failed to initialize LLama model: {ex.Message}", ex);
        }
    }

    public async Task<List<ModelData>> LoadModelsAsync()
    {
        // For local models, we might want to scan a directory for model files
        // For now, returning a dummy model list
        var models = new List<ModelData>
        {
            new ModelData { Id = fModelPath },
        };

        return await Task.FromResult(models);
    }

    private static InferenceParams GetInferenceParams(LMSettings settings, float? temperature)
    {
        var inferenceParams = new InferenceParams() {
            SamplingPipeline = new DefaultSamplingPipeline() {
                Temperature = temperature.HasValue ? temperature.Value : (float)settings.Temperature,
                TopP = (float)settings.TopP,
                PresencePenalty = (float)settings.PresencePenalty,
                FrequencyPenalty = (float)settings.FrequencyPenalty,
            },
            MaxTokens = settings.MaxTokens
        };
        return inferenceParams;
    }

    private static ChatHistory.Message CreateMessage(string role, string content)
    {
        AuthorRole authorRole;
        switch (role) {
            case "system":
                authorRole = AuthorRole.System; break;
            case "user":
                authorRole = AuthorRole.User; break;
            case "assistant":
                authorRole = AuthorRole.Assistant; break;
            default:
                authorRole = AuthorRole.Unknown; break;
        }
        var result = new ChatHistory.Message(authorRole, content);
        return result;
    }

    /// <summary>
    /// Single request (waiting for the model's complete response).
    /// </summary>
    public async Task<string> SendMessageSingleAsync(string role, string content, float temperature)
    {
        InitializeModel();

        var settings = fChat.Settings;

        try {
            var inferenceParams = GetInferenceParams(settings, temperature);
            var executor = new InteractiveExecutor(fContext);
            var chatSession = new ChatSession(executor);
            var message = CreateMessage(role, content);

            var result = new StringBuilder();
            await foreach (var text in chatSession.ChatAsync(message, inferenceParams)) {
                result.Append(text);
            }
            return result.ToString();
        } catch (Exception ex) {
            throw new InvalidOperationException($"Failed to generate response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Request.
    /// </summary>
    public async Task SendMessageAsync()
    {
        InitializeModel();

        var settings = fChat.Settings;
        fChat.RequestId += 1;
        fTokenSource.TryReset();

        try {
            var inferenceParams = GetInferenceParams(settings, null);
            var executor = new InteractiveExecutor(fContext);
            var chatSession = new ChatSession(executor);

            var chatHistory = new ChatHistory();
            BuildChatHistory(chatHistory, fChat.HistoryStorage.CurrentHistory);

            /*if (!settings.StreamMode)*/
            {
                // Regular request (waiting for the model's complete response).
                var result = new StringBuilder();
                await foreach (var text in chatSession.ChatAsync(chatHistory, inferenceParams, fTokenSource.Token)) {
                    result.Append(text);
                }
                var message = new ChatMessage("assistant", result.ToString());
                await ProcessReceivedMessage(message);
            }/* else {
                // Streaming request (streaming tokens as they are generated).
                var result = new StringBuilder();

                try {
                    fChat.View?.StartStreamingMessage(fChat.RequestId);

                    await foreach (var text in chatSession.ChatAsync(prompt, inferenceParams, fTokenSource.Token)) {
                        result.Append(text);
                        fChat.View?.UpdateStreamingMessage(fChat.RequestId, result.ToString());

                        // Check for cancellation
                        if (fTokenSource.Token.IsCancellationRequested)
                            break;
                    }
                } finally {
                    // Add the complete assistant message to history
                    await fChat.AddHistory("assistant", result.ToString());
                    fChat.View?.FinalizeStreamingMessage(fChat.RequestId);
                }
            }*/
        } catch (Exception ex) {
            // Add error message to history
            await fChat.AddHistory("assistant", $"Error: {ex.Message}");
            fChat.View?.FinalizeStreamingMessage(fChat.RequestId);
            throw new InvalidOperationException($"Failed to generate response: {ex.Message}", ex);
        }
    }

    private void BuildChatHistory(ChatHistory chatHistory, List<ChatMessage> history)
    {
        // Add conversation history
        foreach (var message in history) {
            AuthorRole authorRole;
            switch (message.Role) {
                case "system":
                    authorRole = AuthorRole.System; break;
                case "user":
                    authorRole = AuthorRole.User; break;
                case "assistant":
                    authorRole = AuthorRole.Assistant; break;
                default:
                    authorRole = AuthorRole.Unknown; break;
            }
            chatHistory.AddMessage(authorRole, message.Content);
        }
    }

    private async Task ProcessReceivedMessage(ChatMessage message)
    {
        // Add assistant's message to history (null if tool_calls are present)
        if (!string.IsNullOrEmpty(message.Content)) {
            await fChat.ProcessMessage(message);
        }

        // Check for tool calls
        if (message.ToolCalls != null && message.ToolCalls.Count > 0) {
            // Process tool calls
            //await ProcessToolCallsAsync(message.ToolCalls);
        }
    }
}

#endif
