/*
 *  BSLib.LMKit, the kit of tools for working with LLM, MCP and RAG.
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

namespace BSLib.LMKit.Services;

public class FileSystemService : IFileSystem
{
    private readonly HashSet<string> _allowedDirectories;

    /// <param name="allowedDirectories">Список разрешенных путей (песочница)</param>
    public FileSystemService(IEnumerable<string> allowedDirectories)
    {
        if (allowedDirectories == null || !allowedDirectories.Any()) {
            throw new ArgumentException("Must allow at least one directory.");
        }

        _allowedDirectories = allowedDirectories
            .Select(Path.GetFullPath)
            .ToHashSet();
    }

    // Проверка, находится ли файл внутри разрешенных директорий (защита от Path Traversal)
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

    // 1. read_file
    public string ReadFile(string path)
    {
        string validPath = ValidateAndGetPath(path);
        return File.ReadAllText(validPath, Encoding.UTF8);
    }

    // 2. write_file
    public void WriteFile(string path, string content)
    {
        string validPath = ValidateAndGetPath(path);
        string directory = Path.GetDirectoryName(validPath);
        if (directory != null && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(validPath, content, Encoding.UTF8);
    }

    // 3. create_directory
    public void CreateDirectory(string path)
    {
        string validPath = ValidateAndGetPath(path);
        Directory.CreateDirectory(validPath);
    }

    // 4. list_directory
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

    // 5. move_file
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

    // 6. grep_search
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

    // 7. get_file_info
    public List<string> GetFileInfo(string path)
    {
        string validPath = ValidateAndGetPath(path);
        var info = new List<string>();

        if (File.Exists(validPath)) {
            var fileInfo = new FileInfo(validPath);
            info.Add($"type: file");
            info.Add($"size_bytes: {fileInfo.Length}");
            info.Add($"created: {fileInfo.CreationTimeUtc}");
            info.Add($"modified: {fileInfo.LastWriteTimeUtc}");
        } else if (Directory.Exists(validPath)) {
            var dirInfo = new DirectoryInfo(validPath);
            info.Add($"type: directory");
            info.Add($"created: {dirInfo.CreationTimeUtc}");
            info.Add($"modified: {dirInfo.LastWriteTimeUtc} ");
        } else {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        return info;
    }
}
