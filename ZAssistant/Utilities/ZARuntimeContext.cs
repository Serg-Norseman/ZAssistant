/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using Microsoft.Extensions.Configuration;
using ZLMKit.MCP;
using ZLMKit.Utilities;
using ZLMTools;

namespace ZAssistant.Utilities;


public class ZARuntimeContext : RuntimeContext
{
    public string DataPath { get; private set; }


    public ZARuntimeContext(IMCPServer mcpServer) : base(mcpServer)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(SysUtils.GetBinPath())
            .AddJsonFile("appsettings.json")
            .Build();

        var dbPath = config.GetSection("Assistant:Path").Value;
        DataPath = dbPath;
    }
}
