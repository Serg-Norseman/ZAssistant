/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace ZAssistant.Forms;


public static class UIHelper
{
    /// <summary>
    /// Converts markdown tables to HTML tables
    /// </summary>
    public static string ConvertMarkdownTablesToHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Regular expression to find markdown tables
        // Looks for tables that start with a header (| ... |) 
        // and have a separator line (|----|----|)
        var tableRegex = new Regex(
            @"(\|(?:\s*.*?\s*\|)+)\s*\n\s*(\|(?:\s*:?-+:?\s*\|)+)\s*\n((?:\s*\|(?:\s*.*?\s*\|)+\s*\n?)*)",
            RegexOptions.Multiline);

        return tableRegex.Replace(text, match => {
            var headerLine = match.Groups[1].Value.Trim();
            var separatorLine = match.Groups[2].Value.Trim();
            var dataLines = match.Groups[3].Value.Trim();

            // Create HTML table
            var html = new StringBuilder();
            html.AppendLine("<table>");

            // Process header
            html.AppendLine("  <tr>");
            var headers = headerLine.Split('|');
            // Skip first and last empty elements
            for (int i = 1; i < headers.Length - 1; i++) {
                html.AppendLine($"    <th>{headers[i].Trim()}</th>");
            }
            html.AppendLine("  </tr>");

            // Process data rows
            var lines = dataLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines) {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                html.AppendLine("  <tr>");
                var cells = trimmedLine.Split('|');
                for (int i = 1; i < cells.Length - 1; i++) // Skip first and last empty elements
                {
                    html.AppendLine($"    <td>{cells[i].Trim()}</td>");
                }
                html.AppendLine("  </tr>");
            }

            html.AppendLine("</table>");
            return html.ToString();
        });
    }


    public static string ConvertMarkdownToHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().ConfigureNewLine("\n").Build();

        return Markdown.ToHtml(text, pipeline);
    }
}
