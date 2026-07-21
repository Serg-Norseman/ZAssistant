/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.IO;
using System.Text;
using BSLib;
using Microsoft.Extensions.Configuration;
using ZLMKit;
using ZLMKit.Database;
using ZLMKit.MCP;
using ZLMKit.Services;
using ZLMKit.Utilities;

namespace ZLMTools;

public class RuntimeContext : IRuntimeContext
{
    private readonly IMCPServer fMCPServer;
    private readonly FileSystemService fFileSystem;

    public string DefaultTimeFormat { get { return "yyyy-MM-dd HH:mm:ss"; } }

    public bool MemoryEnabled { get; private set; }

    public bool ProfileEnabled { get; private set; }

    public bool TasksEnabled { get; private set; }

    public bool FTSEnabled { get; private set; }

    public IMCPServer MCPServer
    {
        get { return fMCPServer; }
    }

    public RuntimeContext(IMCPServer mcpServer)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(SysUtils.GetBinPath())
            .AddJsonFile("appsettings.json")
            .Build();

        bool dbEnabled = false;
        bool.TryParse(config.GetSection("LocalDatabase:Enabled").Value, out dbEnabled);
        var dbPath = config.GetSection("LocalDatabase:Path").Value;
        if (dbEnabled) LLMDatabase.SetDBPath(dbPath);

        string strAllowedDirectories = config.GetSection("FileSystem:AllowedDirectories").Value;
        var allowedDirectories = strAllowedDirectories.Split(';');

        bool memoryEnabled = false;
        bool.TryParse(config.GetSection("LocalDatabase:MemoryEnabled").Value, out memoryEnabled);
        MemoryEnabled = memoryEnabled;

        bool profileEnabled = false;
        bool.TryParse(config.GetSection("LocalDatabase:ProfileEnabled").Value, out profileEnabled);
        ProfileEnabled = profileEnabled;

        bool tasksEnabled = false;
        bool.TryParse(config.GetSection("LocalDatabase:TasksEnabled").Value, out tasksEnabled);
        TasksEnabled = tasksEnabled;

        bool ftsEnabled = false;
        bool.TryParse(config.GetSection("LocalDatabase:FTSEnabled").Value, out ftsEnabled);
        FTSEnabled = ftsEnabled;

        fMCPServer = mcpServer;
        fFileSystem = new FileSystemService(allowedDirectories);
    }

    public T Get<T>() where T : class
    {
        var typeToResolve = typeof(T);

        if (typeToResolve == typeof(IMCPServer)) {
            return fMCPServer as T;
        } else
        if (typeToResolve == typeof(IFileSystem)) {
            return fFileSystem as T;
        } else
        if (typeToResolve == typeof(ILogger)) {
            return Logger.GetLogger() as T;
        }

        return null;
    }

    public static void Initialize()
    {
        // Without this, an attempt to save a file with a non-Latin name
        // resulted in the name appearing in 866 encoding (system default).
        try {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        } catch (IOException) {
            // The console is redirected or unavailable
        }

        Logger.Init(Path.Combine(SysUtils.GetBinPath(), "ZLMTools.log"));
        ZLMKit.MCP.MCPServer.SetLogger(Logger.GetLogger());
    }
}
