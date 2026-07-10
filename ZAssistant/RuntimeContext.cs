/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using BSLib.LMKit;
using BSLib.LMKit.MCP;
using BSLib.LMKit.Services;

namespace ZAssistant;

public class RuntimeContext : IRuntimeContext
{
    private readonly IMCPServer fMCPServer;
    private readonly FileSystemService fFileSystem;

    public RuntimeContext(IMCPServer mcpServer, IEnumerable<string> allowedDirectories)
    {
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
        }

        return null;
    }
}
