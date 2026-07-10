/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.IO;

namespace ZAssistant
{
    internal class SysUtils
    {
        public static string GetBinPath()
        {
#if NET6_0_OR_GREATER
            string fn = Environment.ProcessPath;
#else
            var asm = SysUtils.GetExecutingAssembly();
            Module[] mods = asm.GetModules();
            string fn = mods[0].FullyQualifiedName;
#endif
            return Path.GetDirectoryName(fn) + Path.DirectorySeparatorChar;
        }
    }
}
