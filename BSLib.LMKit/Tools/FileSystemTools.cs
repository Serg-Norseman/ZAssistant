/*
 *  BSLib.LMKit, the kit of tools for working with LLM, MCP and RAG.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BSLib.LMKit.MCP;
using BSLib.LMKit.Protocols;
using BSLib.LMKit.Services;

namespace BSLib.LMKit.Tools;

internal class ReadFileTool : BaseTool
{
    public ReadFileTool() : base("read_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Read the complete contents of a file from the file system. Returns the file contents as a string.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the file to read" }
                },
                Required = ["path"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            string content = context.Get<IFileSystem>().ReadFile(path);
            return await MCPContent.CreateSimpleContentAsync(content);
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error reading file: {ex.Message}");
        }
    }
}

internal class WriteFileTool : BaseTool
{
    public WriteFileTool() : base("write_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Create a new file or overwrite an existing file with the provided content.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path where the file should be written" },
                    ["content"] = new MCPToolProperty { Type = "string", Description = "The text content to write into the file" }
                },
                Required = ["path", "content"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string content = MCPHelper.GetRequiredStr(args, "content");

        try {
            context.Get<IFileSystem>().WriteFile(path, content);
            return await MCPContent.CreateSimpleContentAsync($"File written successfully: {path}");
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error writing file: {ex.Message}");
        }
    }
}

internal class CreateDirectoryTool : BaseTool
{
    public CreateDirectoryTool() : base("create_directory") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Create a new directory at the specified path. If parent directories do not exist, they will be created.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the directory to create" }
                },
                Required = ["path"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            context.Get<IFileSystem>().CreateDirectory(path);

            return await MCPContent.CreateSimpleContentAsync($"Directory created successfully: {path}");
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error creating directory: {ex.Message}");
        }
    }
}

internal class ListDirectoryTool : BaseTool
{
    public ListDirectoryTool() : base("list_directory") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "List the contents of a directory, returning a list of directory and file names.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the directory to list" }
                },
                Required = ["path"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            var result = context.Get<IFileSystem>().ListDirectory(path);
            return await Task.FromResult(result.Select(x => new MCPContent { Text = $"{x}" }).ToList());
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error listing directory: {ex.Message}");
        }
    }
}

internal class MoveFileTool : BaseTool
{
    public MoveFileTool() : base("move_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Move or rename a file or directory from a source path to a destination path.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["source"] = new MCPToolProperty { Type = "string", Description = "The current path of the file or directory" },
                    ["destination"] = new MCPToolProperty { Type = "string", Description = "The new path where the file or directory should be moved" }
                },
                Required = ["source", "destination"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string source = MCPHelper.GetRequiredStr(args, "source");
        string destination = MCPHelper.GetRequiredStr(args, "destination");

        try {
            context.Get<IFileSystem>().MoveFile(source, destination);
            return await MCPContent.CreateSimpleContentAsync($"File moved from {source} to {destination}");
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error moving file/directory: {ex.Message}");
        }
    }
}

internal class GrepSearchTool : BaseTool
{
    public GrepSearchTool() : base("grep_search") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Search for a specific regular expression pattern within a file or recursively across all files in a directory.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The file or directory path to search within" },
                    ["pattern"] = new MCPToolProperty { Type = "string", Description = "The regular expression pattern to search for (case-insensitive)" }
                },
                Required = ["path", "pattern"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string pattern = MCPHelper.GetRequiredStr(args, "pattern");

        try {
            var result = context.Get<IFileSystem>().GrepSearch(path, pattern);
            return await Task.FromResult(result.Select(x => new MCPContent { Text = $"{x}" }).ToList());
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error during search: {ex.Message}");
        }
    }
}

internal class GetFileInfoTool : BaseTool
{
    public GetFileInfoTool() : base("get_file_info") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Retrieve metadata about a file or directory, including its type (file/directory), size in bytes, creation time, and modification time.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the file or directory to inspect" }
                },
                Required = ["path"]
            }
        };
    }

    public override async Task<List<MCPContent>> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            var result = context.Get<IFileSystem>().GetFileInfo(path);

            return await MCPContent.CreateSimpleContentAsync(string.Join("\n", result));
        } catch (System.Exception ex) {
            return await MCPContent.CreateSimpleContentAsync($"Error getting file info: {ex.Message}");
        }
    }
}
