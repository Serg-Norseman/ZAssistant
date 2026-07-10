/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;

namespace ZAssistant
{
    public interface IFileSystem
    {
        void CreateDirectory(string path);
        List<string> GetFileInfo(string path);
        List<string> GrepSearch(string path, string pattern);
        List<string> ListDirectory(string path);
        void MoveFile(string source, string destination);
        string ReadFile(string path);
        void WriteFile(string path, string content);
    }
}
