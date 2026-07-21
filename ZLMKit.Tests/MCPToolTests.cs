using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ZLMKit.Database;
using ZLMKit.LMChat;
using ZLMKit.MCP;
using ZLMKit.Protocols;
using ZLMKit.Utilities;

namespace ZLMKit.Tests;

public class MCPToolTests
{
    protected readonly IRuntimeContext fContext;

    static MCPToolTests()
    {
        //LangMan.DefInit();
        //MCPController.InitFeatures(embedded: false, pureMode: false, tdeMode: true, ragMode: true);
        LLMDatabase.SetDBPath(Path.Combine(SysUtils.GetBinPath(), "test.db"));
    }

    public MCPToolTests()
    {
        fContext = new TestRuntimeContext();
    }

    public static List<MCPContent> ExecTool(BaseTool tool, IRuntimeContext context, string jsonString)
    {
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        return tool.ExecuteTool(context, args);
    }
}


public class TestRuntimeContext : IRuntimeContext
{
    public IMCPServer Server { get; } = null;
    public ILMChat Client { get; } = null;

    public string DefaultTimeFormat { get { return "yyyy-MM-dd HH:mm:ss"; } }

    public bool MemoryEnabled => true;

    public bool ProfileEnabled => true;

    public bool TasksEnabled => true;

    public bool FTSEnabled { get; set; }

    public IMCPServer MCPServer { get; set; }

    public TestRuntimeContext()
    {
        FTSEnabled = true;
    }

    public T Get<T>() where T : class
    {
        return null;
    }
}
