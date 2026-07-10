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
using BSLib.LMKit.MCP;
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

        Logger.Init(Path.Combine(SysUtils.GetBinPath(), "ZAssistant.log"));
        MCPServer.SetLogger(Logger.GetLogger());

        // Check if running in `Tool Discovery & Execution` mode
        bool tdeMode = Array.IndexOf(args, "--tde") >= 0;

        // Check if running in `Retrieval-Augmented Generation` mode
        bool ragMode = Array.IndexOf(args, "--rag") >= 0;

        try {
            var config = new ConfigurationBuilder()
                .SetBasePath(SysUtils.GetBinPath())
                .AddJsonFile("appsettings.json")
                .Build();

            var dbPath = config.GetSection("LocalDatabase:Path").Value;
            //LLMDatabase.SetAppDataPath(dbPath);

            string strAllowedDirectories = config.GetSection("FileSystem:AllowedDirectories").Value;
            var allowedDirectories = strAllowedDirectories.Split(';');

            var server = new MCPServer();
            server.Context = new RuntimeContext(server, allowedDirectories);
            server.InitFeatures(tdeMode, ragMode);
            server.Run();
        } catch (Exception ex) {
            MCPServer.Log($"Fatal error during initialization: {ex}");
            Environment.Exit(1);
        }
    }
}
