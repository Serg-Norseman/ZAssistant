/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BSLib;
using ZLMKit.MCP;
using ZLMKit.Protocols;
using ZLMKit.Services;

namespace ZLMKit.Tools;


internal abstract class FileTool : BaseTool
{
    protected FileTool(string sign) : base(sign) { }

    protected static List<MCPContent> ReadImageFile(IFileSystem fileSystem, string path, string ext)
    {
        string mimeType = "image/" + ((ext == ".png") ? "png" : "jpeg");

        using (var stream = fileSystem.ReadStream(path))
            return MCPHelper.CreateImageContent(stream, mimeType, [Role.User, Role.Assistant], 0.5f);
    }
}


internal class ReadTextFileTool : FileTool
{
    public ReadTextFileTool() : base("read_text_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Read the complete contents of a text file from the file system, or read a portion of it using offset and limit parameters. Returns the file contents as a string (UTF-8).",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the text file to read" },
                    ["offset"] = new MCPToolProperty { Type = "integer", Description = "Optional: The line number to start reading from (0-based). Requires 'limit' to be set." },
                    ["limit"] = new MCPToolProperty { Type = "integer", Description = "Optional: Maximum number of lines to read. Use with 'offset' to paginate through large files." }
                },
                Required = ["path"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        int offset = MCPHelper.GetOptionalInt(args, "offset", -1);
        int limit = MCPHelper.GetOptionalInt(args, "limit", -1);

        var fileSystem = context.Get<IFileSystem>();

        try {
            if (offset >= 0 && limit > 0) {
                string content = fileSystem.ReadFile(path, offset, limit);
                return MCPContent.CreateSimpleContent(content);
            } else {
                string content = fileSystem.ReadFile(path);
                return MCPContent.CreateSimpleContent(content);
            }
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error reading text file: {ex.Message}");
        }
    }
}

internal class ReadImageFileTool : FileTool
{
    public ReadImageFileTool() : base("read_image_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Read an image file from the file system. Returns base64 encoding of the binary contents of a file.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the image file to read (PNG, JPG, JPEG)" }
                },
                Required = ["path"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        var fileSystem = context.Get<IFileSystem>();

        try {
            string ext = FileHelper.GetFileExtension(path);
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") {
                return MCPContent.CreateSimpleContent($"❌ File is not a supported image format (PNG, JPG, JPEG): {path}");
            }

            return ReadImageFile(fileSystem, path, ext);
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error reading image file: {ex.Message}");
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

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string content = MCPHelper.GetRequiredStr(args, "content");

        try {
            context.Get<IFileSystem>().WriteFile(path, content);
            return MCPContent.CreateSimpleContent($"✅ File written successfully: {path}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error writing file: {ex.Message}");
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
            Description = "Create a new directory at the specified path. If parent directories do not exist, they will be created. The tool operates in an isolated space, with permission to perform all actions.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the directory to create" }
                },
                Required = ["path"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            context.Get<IFileSystem>().CreateDirectory(path);

            return MCPContent.CreateSimpleContent($"✅ Directory created successfully: {path}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error creating directory: {ex.Message}");
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

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            var result = context.Get<IFileSystem>().ListDirectory(path);
            return result.Select(x => new MCPContent { Text = $"{x}" }).ToList();
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error listing directory: {ex.Message}");
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
            Description = "Move or rename a file or directory from a source path to a destination path. If the destination file exists, it will be overwritten.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["source"] = new MCPToolProperty { Type = "string", Description = "The current path of the file or directory" },
                    ["destination"] = new MCPToolProperty { Type = "string", Description = "The new path where the file or directory should be moved" }
                },
                Required = ["source", "destination"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string source = MCPHelper.GetRequiredStr(args, "source");
        string destination = MCPHelper.GetRequiredStr(args, "destination");

        try {
            context.Get<IFileSystem>().MoveFile(source, destination);
            return MCPContent.CreateSimpleContent($"✅ File moved from {source} to {destination}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error moving file/directory: {ex.Message}");
        }
    }
}

internal class SearchFilesTool : BaseTool
{
    public SearchFilesTool() : base("search_files") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Search for a specific regular expression pattern within a file or recursively across all files in a directory. A list of files is returned with the numbers and contents of the lines where a match was found.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The file or directory path to search within" },
                    ["pattern"] = new MCPToolProperty { Type = "string", Description = "The regular expression pattern to search for (case-insensitive)" }
                },
                Required = ["path", "pattern"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string pattern = MCPHelper.GetRequiredStr(args, "pattern");

        try {
            var result = context.Get<IFileSystem>().GrepSearch(path, pattern);
            return result.Select(x => new MCPContent { Text = $"{x}" }).ToList();
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error during search: {ex.Message}");
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
            Description = "Retrieve metadata about a file or directory, including its type (file/directory), size in bytes, creation and modification time in formatted strings.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the file or directory to inspect" }
                },
                Required = ["path"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");

        try {
            var result = context.Get<IFileSystem>().GetFileInfo(context, path);

            return MCPContent.CreateSimpleContent(string.Join("\n", result));
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error getting file info: {ex.Message}");
        }
    }
}


internal class AppendFileTool : BaseTool
{
    public AppendFileTool() : base("append_file") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Appends the provided content to the very end of an existing file.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the file to append to." },
                    ["content"] = new MCPToolProperty { Type = "string", Description = "The text content to add (a line or block)." }
                },
                Required = ["path", "content"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string content = MCPHelper.GetRequiredStr(args, "content");
        try {
            context.Get<IFileSystem>().AppendToFile(path, content);

            return MCPContent.CreateSimpleContent($"✅ Successfully appended data to: {path}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error appending to file '{path}': {ex.Message}");
        }
    }
}


internal class EditFileTool : BaseTool
{
    public EditFileTool() : base("edit_file") { }
    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Searches for a regex pattern in a file and replaces all occurrences with the provided replacement string.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["path"] = new MCPToolProperty { Type = "string", Description = "The path of the file to modify." },
                    ["pattern"] = new MCPToolProperty { Type = "string", Description = "The regular expression pattern to search for (e.g., 'old_value')." },
                    ["replacement"] = new MCPToolProperty { Type = "string", Description = "The string that will replace every match of the pattern." }
                },
                Required = ["path", "pattern", "replacement"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string path = MCPHelper.GetRequiredStr(args, "path");
        string pattern = MCPHelper.GetRequiredStr(args, "pattern");
        string replacement = MCPHelper.GetRequiredStr(args, "replacement");
        try {
            bool success = context.Get<IFileSystem>().UpdateInFile(path, pattern, replacement);

            if (success) {
                return MCPContent.CreateSimpleContent($"✅ Successfully updated file '{path}'. Pattern matched and replaced.");
            } else {
                return MCPContent.CreateSimpleContent($"❌ Update failed for file '{path}'. No matches found for pattern: {pattern}.");
            }
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error updating file '{path}': {ex.Message}");
        }
    }
}


internal class ReadMultipleFilesTool : FileTool
{
    public ReadMultipleFilesTool() : base("read_multiple_files") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Read the complete contents of multiple files from the file system. Returns the contents of each file as separate text blocks, with the file name heading.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["paths"] = new MCPToolProperty {
                        Type = "array",
                        Description = "An array of file paths to read",
                        Items = new MCPToolProperty { Type = "string" }
                    }
                },
                Required = ["paths"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string[] paths = MCPHelper.GetRequiredStringArray(args, "paths");
        var resultContents = new List<MCPContent>();

        var fileSystem = context.Get<IFileSystem>();

        try {
            foreach (var path in paths) {
                if (string.IsNullOrEmpty(path)) continue;

                string ext = FileHelper.GetFileExtension(path);
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") {
                    var imageContents = ReadImageFile(fileSystem, path, ext);
                    resultContents.AddRange(imageContents);
                } else {
                    string content = fileSystem.ReadFile(path);
                    resultContents.Add(new MCPContent { Text = $"--- Content of {path} ---\n{content}" });
                }
            }

            return resultContents;
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error reading files: {ex.Message}");
        }
    }
}
