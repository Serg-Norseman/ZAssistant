/*
 *  BSLib.LMKit, the kit of tools for working with LLM, MCP and RAG.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BSLib.LMKit.Protocols;

namespace BSLib.LMKit.MCP;

/// <summary>
/// Abstract base class for MCP-tools.
/// </summary>
public abstract class BaseTool
{
    private readonly string fSign;

    /// <summary>
    /// Gets the internal identifier of the command.
    /// </summary>
    public string Sign
    {
        get { return fSign; }
    }

    public BaseTool()
    {
    }

    /// <summary>
    /// Constructor for the command.
    /// </summary>
    /// <param name="sign">Internal identifier of the command.</param>
    protected BaseTool(string sign)
    {
        fSign = sign;
    }


    /// <summary>
    /// Creates an MCP tool object to support the built-in MCP server.
    /// </summary>
    public virtual MCPTool CreateTool()
    {
        return null;
    }

    /// <summary>
    /// Runs MCP tool to support built-in MCP server.
    /// </summary>
    public virtual async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        return await Task.FromResult(new List<MCPContent>());
    }
}
