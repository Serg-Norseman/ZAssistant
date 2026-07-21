/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Reflection;

namespace ZAssistant.Utilities
{
    /// <summary>
    /// The interface for objects that provide localized resources.
    /// </summary>
    public interface ILangMan
    {
        event Action LanguageChanged;

        string LS(Enum lsid);
        bool LoadFromFile(string fileName, Assembly resAssembly);
    }
}
