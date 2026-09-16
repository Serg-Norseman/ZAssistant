/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using SQLite;

namespace ZLMKit.Database;

[Table("assistant_summary")]
public class AssistantSummary
{
    [Column("session_id"), PrimaryKey]
    public string SessionId { get; set; }

    [Column("global_summary")]
    public string GlobalSummary { get; set; } = string.Empty;

    [Column("session_summary")]
    public string SessionSummary { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
