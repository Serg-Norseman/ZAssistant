/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using ZLMKit.LMChat;

namespace ZAssistant.Utilities;


public class FileSystemSettings
{
    public string AllowedDirectories { get; set; }
}


public class LocalDatabaseSettings
{
    public bool Enabled { get; set; }
    public string Path { get; set; }
    public bool Secure { get; set; }
    public bool MemoryEnabled { get; set; }
    public bool ProfileEnabled { get; set; }
    public bool TasksEnabled { get; set; }
    public bool FTSEnabled { get; set; }
}


public class AppSettings
{
    public FileSystemSettings FileSystem { get; set; }

    public LocalDatabaseSettings LocalDatabase { get; set; }

    public LMSettings Assistant { get; set; }
}
