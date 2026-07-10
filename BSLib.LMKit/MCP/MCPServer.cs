/*
 *  BSLib.LMKit, the kit of tools for working with LLM, MCP and RAG.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BSLib.LMKit.LMChat;
using BSLib.LMKit.Protocols;
using BSLib.LMKit.Services;
using BSLib.LMKit.Tools;

namespace BSLib.LMKit.MCP;


public interface IMCPServer
{
    ILMChat Chat { get; }

    Task<List<MCPContent>> ExecuteTool(string toolName, JsonElement args);
}


/// <summary>
/// Minimal MCP server that reads JSON-RPC 2.0 messages from stdin and writes responses to stdout.
/// No external packages — only System.Text.Json from .NET 8.
/// </summary>
public class MCPServer : IMCPServer
{
    private readonly CancellationTokenSource fCancellationToken;
    private readonly JsonSerializerOptions fJsonOptions;
    private static ILogger fLogger;
    private readonly int fPId;
    private readonly MCPToolsListResult fToolsList;
    private readonly MCPResourcesListResult fResourcesList;
    private readonly MCPPromptsListResult fPromptsList;

    private ILMChat fChat = null;
    private IRuntimeContext fContext;
    private readonly List<MCPTool> fMCPTools = new List<MCPTool>();
    private readonly Dictionary<string, BaseResource> fResources = new Dictionary<string, BaseResource>();
    private readonly Dictionary<string, BaseTool> fTools = new Dictionary<string, BaseTool>();
    private bool fTDE = false;

    public ILMChat Chat
    {
        get { return fChat; }
        set { fChat = value; }
    }

    public IRuntimeContext Context
    {
        get { return fContext; }
        set { fContext = value; }
    }

    public bool KeepAliveTicks { get; set; }
    public bool RequestsLog { get; set; }

    public List<MCPTool> MCPTools
    {
        get { return fMCPTools; }
    }

    public MCPServer()
    {
        fPId = Environment.ProcessId;
        fCancellationToken = new CancellationTokenSource();

        // Jan, LM Studio clients terminate the process in such a way
        // that this handlers is not called.
        AppDomain.CurrentDomain.ProcessExit += (s, e) => {
            Log($"[EXIT] Process {fPId} terminated. Code: {Environment.ExitCode}\n");
        };
        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            fCancellationToken.Cancel();
            Log($"[SIGINT] Interrupt signal received for PID: {fPId}\n");
        };

        var utf8NoBom = new UTF8Encoding(false);
        // Important: disable buffering so that messages are sent instantly.
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8NoBom) { AutoFlush = true, NewLine = "\n" });

        Log($"Initializing MCP server ({fPId})...");

        // The list is generated after registration.
        fToolsList = new MCPToolsListResult();

        // Initialize resources
        fResourcesList = new MCPResourcesListResult();

        // Initialize prompts (minimal stub)
        fPromptsList = new MCPPromptsListResult() {
            Prompts = new List<MCPPrompt>()
        };

        fJsonOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        fJsonOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        );
    }

    private void UpdateQueryableLists()
    {
        fToolsList.Tools = fMCPTools;
        fResourcesList.Resources = fResources.Values.Select(x => x.CreateResource()).ToList();
    }

    public static void SetLogger(ILogger logger)
    {
        fLogger = logger;
    }

    /// <summary>
    /// Main server loop: reads lines from stdin, processes JSON-RPC requests, writes responses to stdout.
    /// </summary>
    public async void Run()
    {
        Log("MCP Server started");

        try {
            if (KeepAliveTicks) {
                _ = Task.Run(async () => {
                    while (!fCancellationToken.IsCancellationRequested) {
                        Log($"Keep-alive tick process {fPId}");
                        await Task.Delay(30000);
                    }
                    Log("Cancelled");
                });
            }

            int nullsBeforeExit = 0;
            var stdin = Console.In;
            while (!fCancellationToken.IsCancellationRequested) {
                try {
                    // If line == null -> MCP client closed the stream.
                    string line = await stdin.ReadLineAsync();
                    if (line == null) {
                        // 10 attempts to check for a possible failure,
                        // although if the stream is already closed, it's pointless.
                        nullsBeforeExit++;
                        if (nullsBeforeExit >= 10) break;
                    }

                    await ProcessRequest(line);
                } catch (Exception ex) {
                    Log($"Error processing request: {ex.Message}");
                    SendResponse(new MCPResponse {
                        Error = MCPError.InternalError(ex.Message)
                    });
                }
            }
        } catch (Exception ex) {
            Log($"Run().Exception: {ex.Message}");
        } finally {
            Log("MCP Server stopped");
        }
    }

    private static bool IsNotificationRequest(MCPRequest request)
    {
        // Notifications don't have an "id" and don't require a response.
        return request?.Id == null || !request.Id.HasValue
            || request.Id.Value.ValueKind == JsonValueKind.Null || request.Id.Value.ValueKind == JsonValueKind.Undefined;
    }

    private async void SendResponse(MCPResponse response)
    {
        var json = JsonSerializer.Serialize(response, fJsonOptions);

        // It definitely doesn't work with `Content-Length` and `WriteAsync()`.
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();

#if DEBUG
        /*string jsonStr = json;
        if (jsonStr.Length > 200) {
            jsonStr = jsonStr.Substring(0, 200);
        }
        Log($"Sending: {jsonStr}");*/
#endif
    }

    public static void Log(string message)
    {
        string line = $"[BSLib.LMKit MCP] {message}";
        fLogger?.WriteInfo(line);

        //Console.Error.WriteLine(line);
        //Console.Error.Flush();
    }

    private async Task ProcessRequest(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        if (RequestsLog)
            Log($"Received: {line}");

        var request = JsonSerializer.Deserialize<MCPRequest>(line, fJsonOptions);

        if (request == null) {
            SendResponse(new MCPResponse {
                Error = MCPError.InvalidParams("Invalid request")
            });
            return;
        }

        if (IsNotificationRequest(request)) {
            // If the MCP-Client sends a notification, it must not be responded to,
            // otherwise it will violate the protocol and cause a session reset.
            return;
        }

        var response = request.Method switch {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCall(request),
            "resources/list" => HandleResourcesList(request),
            "resources/templates/list" => HandleResourceTemplatesList(request),
            "resources/read" => HandleResourceRead(request),
            "prompts/list" => HandlePromptsList(request),
            "prompts/get" => HandlePromptsGet(request),
            _ => new MCPResponse {
                Id = request.Id,
                Error = MCPError.MethodNotFound()
            }
        };

        SendResponse(response);
    }

    public async Task<string> ProcessSSERequestAsync(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;

        if (RequestsLog)
            Log($"Received: {line}");

        var request = JsonSerializer.Deserialize<MCPRequest>(line, fJsonOptions);

        if (request == null) {
            //TODO
            /*SendResponse(new MCPResponse {
                Error = MCPError.InvalidParams("Invalid request")
            });*/
            return string.Empty;
        }

        if (IsNotificationRequest(request)) {
            // If the MCP-Client sends a notification, it must not be responded to,
            // otherwise it will violate the protocol and cause a session reset.
            return string.Empty;
        }

        var response = request.Method switch {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolsCall(request),
            "resources/list" => HandleResourcesList(request),
            "resources/templates/list" => HandleResourceTemplatesList(request),
            "resources/read" => HandleResourceRead(request),
            "prompts/list" => HandlePromptsList(request),
            "prompts/get" => HandlePromptsGet(request),
            _ => new MCPResponse {
                Id = request.Id,
                Error = MCPError.MethodNotFound()
            }
        };

        var json = JsonSerializer.Serialize(response, fJsonOptions);
        return json;
    }

    private static MCPResponse HandleInitialize(MCPRequest request)
    {
        // Actual protocol version = "2025-11-25".
        // FIXME: The version is not simply "specified" - it is negotiated
        // during the initialization process between the client and the server!

        var result = new MCPInitializeResult {
            ProtocolVersion = "2025-11-25", //"2025-06-18", //"2024-11-05",
            Capabilities = new MCPCapabilities {
                Tools = new MCPToolsCapability { ListChanged = false },
                Resources = new MCPResourcesCapability { Subscribe = false, ListChanged = false },
                Prompts = new MCPPromptsCapability { ListChanged = false }
            },
            ServerInfo = new MCPServerInfo {
                Name = "BSLib.LMKit",
                Version = "1.0.0"
            }
        };

        return new MCPResponse { Id = request.Id, Result = result };
    }

    /// <summary>
    /// Returns the list of available MCP tools.
    /// </summary>
    private MCPResponse HandleToolsList(MCPRequest request)
    {
        // Some clients (like Jan) re-request the list of tools very frequently,
        // so it's better to have a cached list in advance.
        return new MCPResponse { Id = request.Id, Result = fToolsList };
    }

    private async Task<MCPResponse> HandleToolsCall(MCPRequest request)
    {
        try {
            if (request.Params == null || request.Params.Value.ValueKind != JsonValueKind.Object) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing params")
                };
            }

            var p = request.Params.Value;
            if (!p.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing tool name")
                };
            }

            string toolName = nameElem.GetString()!;
            p.TryGetProperty("arguments", out var arguments);

            // Execute an MCP tool call by name and arguments.
            var content = await ExecuteTool(toolName, arguments);

            return new MCPResponse {
                Id = request.Id,
                Result = new { Content = content, IsError = false }
            };
        } catch (Exception ex) {
            return new MCPResponse {
                Id = request.Id,
                Result = new {
                    Content = new List<MCPContent> { new MCPContent { Text = ex.Message } },
                    IsError = true
                }
            };
        }
    }

    /// <summary>
    /// Returns the list of available resources.
    /// </summary>
    private MCPResponse HandleResourcesList(MCPRequest request)
    {
        // Minimal stub: return empty list
        return new MCPResponse { Id = request.Id, Result = fResourcesList };
    }

    /// <summary>
    /// Returns the list of resource templates.
    /// </summary>
    private MCPResponse HandleResourceTemplatesList(MCPRequest request)
    {
        // Minimal stub: return empty list
        return new MCPResponse {
            Id = request.Id,
            Result = new MCPResourceTemplatesListResult {
                ResourceTemplates = new List<MCPResourceTemplate>()
            }
        };
    }

    /// <summary>
    /// Reads the content of a specific resource by URI.
    /// </summary>
    private MCPResponse HandleResourceRead(MCPRequest request)
    {
        try {
            if (request.Params == null || request.Params.Value.ValueKind != JsonValueKind.Object) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing params")
                };
            }

            var p = request.Params.Value;
            if (!p.TryGetProperty("uri", out var uriElem) || uriElem.ValueKind != JsonValueKind.String) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing uri")
                };
            }

            string uri = uriElem.GetString()!;

            // Match registered resources
            var resContent = GetResource(uri);
            if (resContent != null) {
                fLogger?.WriteInfo($"Returned contents for resource {uri}");

                return new MCPResponse {
                    Id = request.Id,
                    Result = new {
                        Contents = resContent,
                    }
                };
            }

            // Minimal stub: resource not found
            return new MCPResponse {
                Id = request.Id,
                Error = MCPError.InvalidParams($"Resource not found: {uri}")
            };
        } catch (Exception ex) {
            return new MCPResponse {
                Id = request.Id,
                Error = MCPError.InternalError(ex.Message)
            };
        }
    }

    /// <summary>
    /// Returns the list of available prompts.
    /// </summary>
    private MCPResponse HandlePromptsList(MCPRequest request)
    {
        // Minimal stub: return empty list
        return new MCPResponse { Id = request.Id, Result = fPromptsList };
    }

    /// <summary>
    /// Gets a specific prompt with arguments resolved.
    /// </summary>
    private MCPResponse HandlePromptsGet(MCPRequest request)
    {
        try {
            if (request.Params == null || request.Params.Value.ValueKind != JsonValueKind.Object) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing params")
                };
            }

            var p = request.Params.Value;
            if (!p.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String) {
                return new MCPResponse {
                    Id = request.Id,
                    Error = MCPError.InvalidParams("Missing name")
                };
            }

            string name = nameElem.GetString()!;

            // Minimal stub: prompt not found
            return new MCPResponse {
                Id = request.Id,
                Error = MCPError.InvalidParams($"Prompt not found: {name}")
            };
        } catch (Exception ex) {
            return new MCPResponse {
                Id = request.Id,
                Error = MCPError.InternalError(ex.Message)
            };
        }
    }

    public void InitFeatures(bool tdeMode, bool ragMode)
    {
        fTDE = tdeMode;

        if (fContext.Get<IFileSystem>() != null) {
            // Files operations
            RegisterTool(new ReadFileTool());
            RegisterTool(new WriteFileTool());
            RegisterTool(new CreateDirectoryTool());
            RegisterTool(new ListDirectoryTool());
            RegisterTool(new MoveFileTool());
            RegisterTool(new GrepSearchTool());
            RegisterTool(new GetFileInfoTool());
        }

        if (tdeMode) {
            RegisterTool(new SearchTool(), true);
            RegisterTool(new UseTool(), true);
        }

        if (ragMode) {
            RegisterTool(new RAGSearchExamplesTool(), true);
            RegisterTool(new RAGWritePatternTool(), true);

            RegisterTool(new StoreFactTool(), true);
            RegisterTool(new SearchMemoryTool(), true);

            RegisterTool(new GetContextSummaryTool(), true);
            RegisterTool(new SaveChatMilestoneTool(), true);

            RegisterTool(new GetKnowledgeSubgraphTool(), true);
            RegisterTool(new AddKnowledgeNodeTool(), true);
            RegisterTool(new ConnectKnowledgeNodesTool(), true);

            /*RegisterTool(new CreateGenealogyTaskTool(), true);
            RegisterTool(new UpdateTaskProgressTool(), true);
            RegisterTool(new ChangeTaskStatusTool(), true);*/

            RegisterTool(new GetUserProfileTool(), true);
            RegisterTool(new UpdateUserProfileTool(), true);
            RegisterTool(new RemoveUserPreferenceTool(), true);
        }
    }

    public void RegisterTool(BaseTool tool, bool metaTool = false)
    {
        fTools.Add(tool.Sign, tool);

        MCPTool mcpTool = tool.CreateTool();
        if (mcpTool != null) {
            if (fTDE && !metaTool) {
                MCPToolDiscovery.Register(tool.Sign, mcpTool);
            }

            if (!fTDE || metaTool) {
                fMCPTools.Add(mcpTool);
            }
        }
    }

    public async Task<List<MCPContent>> ExecuteTool(string toolName, JsonElement args)
    {
        if (fTools.TryGetValue(toolName, out BaseTool cmd)) {
            return await cmd.ExecuteTool(fContext, args);
        } else {
            throw new ArgumentException($"Unknown tool: {toolName}");
        }
    }

    public void RegisterResource(BaseResource resource)
    {
        fResources.Add(resource.Uri, resource);
    }

    public List<MCPResourceContents> GetResource(string uri)
    {
        if (fResources.TryGetValue(uri, out BaseResource res)) {
            return res.Get(fContext);
        } else {
            return null;
        }
    }
}
