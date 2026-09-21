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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ZLMKit.Utilities;

namespace ZLMKit.Services;

public class FileSystemService : IFileSystem
{
    private readonly HashSet<string> _allowedDirectories;

    public FileSystemService(IEnumerable<string> allowedDirectories)
    {
        if (allowedDirectories == null || !allowedDirectories.Any()) {
            throw new ArgumentException("Must allow at least one directory.");
        }

        _allowedDirectories = allowedDirectories
            .Select(Path.GetFullPath)
            .ToHashSet();
    }

    private string ValidateAndGetPath(string requestedPath)
    {
        bool isRelative = !Path.IsPathFullyQualified(requestedPath);
        string fullPath = !isRelative ? Path.GetFullPath(requestedPath) : Path.GetFullPath(requestedPath, _allowedDirectories.First());

        bool isAllowed = _allowedDirectories.Any(allowed =>
            fullPath.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed) {
            throw new UnauthorizedAccessException($"Access denied: Path '{requestedPath}' is outside allowed directories.");
        }

        return fullPath;
    }

    public string ReadFile(string path)
    {
        string validPath = ValidateAndGetPath(path);
        return File.ReadAllText(validPath, Encoding.UTF8);
    }

    public string ReadFile(string path, int offset, int limit)
    {
        string validPath = ValidateAndGetPath(path);
        string[] lines = File.ReadAllLines(validPath, Encoding.UTF8);

        if (offset < 0 || offset >= lines.Length) {
            throw new ArgumentOutOfRangeException(nameof(offset), $"Offset {offset} is out of range. File has {lines.Length} lines.");
        }

        if (limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0.");
        }

        int endIndex = Math.Min(offset + limit, lines.Length);
        string[] selectedLines = new string[endIndex - offset];
        Array.Copy(lines, offset, selectedLines, 0, selectedLines.Length);

        return string.Join(Environment.NewLine, selectedLines);
    }

    public Stream ReadStream(string path)
    {
        string validPath = ValidateAndGetPath(path);
        return File.OpenRead(validPath);
    }

    public void WriteFile(string path, string content)
    {
        string validPath = ValidateAndGetPath(path);
        string directory = Path.GetDirectoryName(validPath);
        if (directory != null && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(validPath, content, Encoding.UTF8);
    }

    public void CreateDirectory(string path)
    {
        string validPath = ValidateAndGetPath(path);
        Directory.CreateDirectory(validPath);
    }

    public List<string> ListDirectory(string path)
    {
        string validPath = ValidateAndGetPath(path);
        var entries = new List<string>();

        foreach (var dir in Directory.GetDirectories(validPath)) {
            entries.Add($"[DIR] {Path.GetFileName(dir)}");
        }
        foreach (var file in Directory.GetFiles(validPath)) {
            entries.Add($"[FILE] {Path.GetFileName(file)}");
        }

        return entries;
    }

    public void MoveFile(string source, string destination)
    {
        string validSource = ValidateAndGetPath(source);
        string validDest = ValidateAndGetPath(destination);

        if (File.Exists(validSource)) {
            File.Move(validSource, validDest, overwrite: true);
        } else if (Directory.Exists(validSource)) {
            Directory.Move(validSource, validDest);
        } else {
            throw new FileNotFoundException($"Source path not found: {source}");
        }
    }

    public List<string> GrepSearch(string path, string pattern)
    {
        string validPath = ValidateAndGetPath(path);
        var results = new List<string>();
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var files = Directory.Exists(validPath)
            ? Directory.GetFiles(validPath, "*", SearchOption.AllDirectories)
            : new[] { validPath };

        foreach (var file in files) {
            try {
                string validFilePath = ValidateAndGetPath(file);
                var lines = File.ReadAllLines(validFilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++) {
                    if (regex.IsMatch(lines[i])) {
                        results.Add($"{Path.GetRelativePath(validPath, validFilePath)}: Line {i + 1}: {lines[i].Trim()}");
                    }
                }
            } catch (Exception) {
                // Пропускаем бинарные файлы или файлы без прав доступа, как в оригинальном MCP
            }
        }

        return results;
    }

    public List<string> GetFileInfo(IRuntimeContext context, string path)
    {
        string validPath = ValidateAndGetPath(path);
        var info = new List<string>();

        if (File.Exists(validPath)) {
            var fileInfo = new FileInfo(validPath);
            info.Add($"type: file");
            info.Add($"size_bytes: {fileInfo.Length}");
            info.Add($"created: {fileInfo.CreationTime.ToString(context.DefaultTimeFormat)}");
            info.Add($"modified: {fileInfo.LastWriteTime.ToString(context.DefaultTimeFormat)}");
        } else if (Directory.Exists(validPath)) {
            var dirInfo = new DirectoryInfo(validPath);
            info.Add($"type: directory");
            info.Add($"created: {dirInfo.CreationTime.ToString(context.DefaultTimeFormat)}");
            info.Add($"modified: {dirInfo.LastWriteTime.ToString(context.DefaultTimeFormat)} ");
        } else {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        return info;
    }

    public void AppendToFile(string path, string content)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        string validPath = ValidateAndGetPath(path);
        string contentToAppend = Environment.NewLine + content;
        File.AppendAllText(validPath, contentToAppend);
    }

    public bool UpdateInFile(string path, string pattern, string replacement)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentNullException(nameof(pattern));

        string validPath = ValidateAndGetPath(path);
        string originalContent = File.ReadAllText(validPath);
        Regex regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        string newContent = regex.Replace(originalContent, replacement);
        File.WriteAllText(validPath, newContent);
        return true;
    }
}
