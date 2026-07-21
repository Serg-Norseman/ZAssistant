/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ZLMKit.MCP;
using ZLMKit.Protocols;
using ZLMKit.Providers;

namespace ZLMKit.LMChat;


public interface ILMChatView
{
    void ShowMessage(string msg, string role);

    void StartStreamingMessage(int requestId);
    void UpdateStreamingMessage(int requestId, string content);
    void FinalizeStreamingMessage(int requestId);
}


public interface ILMChat
{
    LMHistoryStorage HistoryStorage { get; }
    MCPServer MCPServer { get; }
    int RequestId { get; set; }
    LMSettings Settings { get; }
    ILMChatView View { get; }

    Task AddHistory(string role, string content);
    Task ProcessMessage(ChatMessage message);
    Task<string> SendMessageSingleAsync(string role, string content, float temperature);
    Task SendMessageAsync();
}


public class LMChatClient : ILMChat
{
    private ILMChatProvider fChatProvider;
    private int fRequestId;
    private string fSystemPrompt;

    public LMHistoryStorage HistoryStorage { get; private set; }

    public MCPServer MCPServer { get; set; }

    public int RequestId
    {
        get { return fRequestId; }
        set { fRequestId = value; }
    }

    public LMSettings Settings { get; set; }

    public string SystemPrompt
    {
        get { return fSystemPrompt; }
        set { fSystemPrompt = value; }
    }

    public ILMChatView View { get; set; }


    public LMChatClient(LMSettings settings)
    {
        fSystemPrompt = "You are an advanced AI assistant for the GEDKeeper genealogy program. You help analyze databases and build family trees.";

        Settings = settings;
        HistoryStorage = new LMHistoryStorage();

        NewSession();

        // Initialize the appropriate provider based on settings
        InitializeProvider(settings);
    }

    private void InitializeProvider(LMSettings settings)
    {
#if LLS
        // For now, we'll use a simple approach to determine the provider
        // In a more robust implementation, you might want a specific setting for this
        if (settings.APIAddress.StartsWith("http")) {
            fChatProvider = new OpenAIChatProvider(this);
        } else {
            // Assume it's a local model path for LLama
            fChatProvider = new LLamaChatProvider(this);
        }
#else
        fChatProvider = new OpenAIChatProvider(this);
#endif
    }

#if LLS
    /// <summary>
    /// Switch the chat provider
    /// </summary>
    public void SwitchProvider(bool useOpenAI)
    {
        if (useOpenAI && !(fChatProvider is OpenAIChatProvider)) {
            fChatProvider = new OpenAIChatProvider(this);
        } else if (!useOpenAI && !(fChatProvider is LLamaChatProvider)) {
            fChatProvider = new LLamaChatProvider(this);
        }
    }
#endif

    /// <summary>
    /// Clear chat history.
    /// </summary>
    public void NewSession()
    {
        HistoryStorage.CreateNewSession("unknown", Settings, fSystemPrompt);
        fRequestId = 0;
    }

    /// <summary>
    /// Cancel current request.
    /// </summary>
    public void CancelRequest()
    {
        fChatProvider.CancelRequest();
    }

    public async Task AddHistory(string role, string content)
    {
        await HistoryStorage.AddToCurrentHistory(new ChatMessage(role, content));
    }

    public async Task AddHistory(string role, List<ContentItem> content)
    {
        await HistoryStorage.AddToCurrentHistory(new ChatMessage(role, content));
    }

    public async Task<List<ModelData>> LoadModelsAsync()
    {
        return await fChatProvider.LoadModelsAsync();
    }

    public async Task ProcessMessage(ChatMessage message)
    {
        if (message == null) return;

        await HistoryStorage.AddToCurrentHistory(message);
        View?.ShowMessage(JsonSerializer.Serialize(message.Content), message.Role);
    }

    /// <summary>
    /// Single request (waiting for the model's complete response).
    /// </summary>
    public async Task<string> SendMessageSingleAsync(string role, string content, float temperature)
    {
        return await fChatProvider.SendMessageSingleAsync(role, content, temperature);
    }

    /// <summary>
    /// Request.
    /// </summary>
    public async Task SendMessageAsync()
    {
        await fChatProvider.SendMessageAsync();
    }
}
