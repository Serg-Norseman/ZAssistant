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
using System.Reflection;
using System.Text;

namespace ZAssistant.Utilities
{
    /// <summary>
    /// Global (static) manager of localizable strings.
    /// </summary>
    public static partial class LangMan
    {
        public const int LS_DEF_CODE = 1033;
        public const string LS_DEF_SIGN = "enu";
        public const string LS_DEF_NAME = "English";

        private static readonly LangManager fLangMan = new LangManager();

        public static string LS(LSID lsid)
        {
            return fLangMan.LS(lsid);
        }

        public static string LS(LSID lsid, params object[] args)
        {
            return string.Format(fLangMan.LS(lsid), args);
        }

        public static bool LoadFromFile(string fileName, Assembly resAssembly)
        {
            return fLangMan.LoadFromFile(fileName, resAssembly);
        }
    }


    /// <summary>
    /// Instantiable manager of localizable strings.
    /// </summary>
    public class LangManager : ILangMan
    {
        private readonly Dictionary<int, string> fList;

        public event Action LanguageChanged;

        public LangManager()
        {
            fList = new Dictionary<int, string>();
        }

        public string LS(Enum lsid)
        {
            int idx = ((IConvertible)lsid).ToInt32(null);
            string res;
            return fList.TryGetValue(idx, out res) ? res : "?";
        }

        public bool LoadFromFile(string fileName, Assembly resAssembly)
        {
            bool result = false;

            if (resAssembly == null && !File.Exists(fileName)) return result;

            using (var inputStream = resAssembly == null ? new FileStream(fileName, FileMode.Open, FileAccess.Read) : resAssembly.GetManifestResourceStream(fileName)) {
                fList.Clear();

                using (StreamReader lngFile = new StreamReader(inputStream, Encoding.UTF8)) {
                    string st = lngFile.ReadLine();
                    if (!string.IsNullOrEmpty(st) && st[0] == ';') {
                        st = st.Remove(0, 1);
                        string[] lngParams = st.Split(',');
                        if (lngParams.Length < 3)
                            throw new Exception("Header is incorrect");
                    }

                    while (lngFile.Peek() != -1) {
                        st = lngFile.ReadLine();
                        if (string.IsNullOrEmpty(st)) continue;

                        // allowed empty and comment strings
                        int commentPos = st.IndexOf(";");
                        if (commentPos >= 0) st = st.Substring(0, commentPos);
                        st = st.Trim();
                        if (string.IsNullOrEmpty(st)) continue;

                        string[] parts = st.Split('=');
                        if (parts.Length == 2) {
                            int i = int.Parse(parts[0]);
                            fList.Add(i, parts[1]);
                        }
                    }
                    result = true;
                }
            }

            LanguageChanged?.Invoke();

            return result;
        }
    }
}
