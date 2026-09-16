/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.IO;

namespace ZLMKit.Utilities
{
    public static class SysUtils
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

        /// <summary>
        /// Computes the Levenshtein edit distance between two strings.
        /// Uses an optimized single-row algorithm with O(min(m,n)) memory.
        /// </summary>
        public static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            // Ensure b is the shorter string for optimal memory usage
            if (a.Length < b.Length)
                (a, b) = (b, a);

            var costs = new int[b.Length + 1];
            for (int i = 0; i <= b.Length; i++) costs[i] = i;

            for (int i = 1; i <= a.Length; i++) {
                int prev = costs[0];
                costs[0] = i;
                for (int j = 1; j <= b.Length; j++) {
                    int current = costs[j];
                    costs[j] = Math.Min(
                        Math.Min(costs[j - 1] + 1, costs[j] + 1),
                        prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                    prev = current;
                }
            }

            return costs[b.Length];
        }
    }
}
