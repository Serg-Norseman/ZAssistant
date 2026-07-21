/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using ZLMKit.MCP;

namespace ZLMKit;

/// <summary>
/// Temporary stupid solution, later switch to DI.
/// </summary>
public interface IRuntimeContext
{
    string DefaultTimeFormat { get; }

    bool MemoryEnabled { get; }

    bool ProfileEnabled { get; }

    bool TasksEnabled { get; }

    bool FTSEnabled { get; }

    IMCPServer MCPServer { get; }

    T Get<T>() where T : class;
}
