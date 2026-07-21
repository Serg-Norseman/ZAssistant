/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using ZLMKit.MCP;

namespace ZLMTools;

internal class Program
{
    static void Main(string[] args)
    {
        try {
            RuntimeContext.Initialize();
            var server = new MCPServer();
            server.Context = new RuntimeContext(server);
            server.InitFeatures(false, false);
            server.Run();
        } catch (Exception ex) {
            MCPServer.Log($"Fatal error during initialization: {ex}");
            Environment.Exit(1);
        }
    }
}
