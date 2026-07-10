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
using System.Text;
using BSLib.LMKit;
using GKCortex.MCP;
using Microsoft.Extensions.Configuration;

namespace ZAssistant;

internal class Program
{
    static void Main(string[] args)
    {
        // Without this, an attempt to save a file with a non-Latin name
        // resulted in the name appearing in 866 encoding (system default).
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

#if NETCOREAPP3_1_OR_GREATER
        // support for legacy encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

        Logger.Init(Path.Combine(SysUtils.GetBinPath(), "ZAssistant.log"));
        MCPServer.SetLogger(Logger.GetLogger());

        // Check if running in `Tool Discovery & Execution` mode
        bool tdeMode = Array.IndexOf(args, "--tde") >= 0;

        // Check if running in `Retrieval-Augmented Generation` mode
        bool ragMode = Array.IndexOf(args, "--rag") >= 0;

        // Common for all modes
        //LLMDatabase.SetAppDataPath(AppHost.GetAppDataPathStatic());

        try {
            var config = new ConfigurationBuilder()
                .SetBasePath(SysUtils.GetBinPath())
                .AddJsonFile("appsettings.json")
                .Build();

            string strAllowedDirectories = config.GetSection("FileSystem:AllowedDirectories").Value;
            var allowedDirectories = strAllowedDirectories.Split(';');

            MCPController.SetContext(new RuntimeContext(allowedDirectories));
            InitFeatures(tdeMode, ragMode);

            var server = new MCPServer();
            server.Run();
        } catch (Exception ex) {
            MCPServer.Log($"Fatal error during initialization: {ex}");
            Environment.Exit(1);
        }
    }

    private static void InitFeatures(bool tdeMode, bool ragMode)
    {
        // Files operations
        MCPController.RegisterTool(new ReadFileTool());
        MCPController.RegisterTool(new WriteFileTool());
        MCPController.RegisterTool(new CreateDirectoryTool());
        MCPController.RegisterTool(new ListDirectoryTool());
        MCPController.RegisterTool(new MoveFileTool());
        MCPController.RegisterTool(new GrepSearchTool());
        MCPController.RegisterTool(new GetFileInfoTool());

        // Common
        MCPController.InitFeatures(tdeMode, ragMode);
    }


    public class RuntimeContext : IRuntimeContext
    {
        public FileSystemService FileSystem { get; private set; }

        public RuntimeContext(IEnumerable<string> allowedDirectories)
        {
            FileSystem = new FileSystemService(allowedDirectories);
        }

        public T Get<T>() where T : class
        {
            var typeToResolve = typeof(T);

            if (typeToResolve == typeof(IFileSystem)) {
                return FileSystem as T;
            }

            return null;
        }
    }
}
