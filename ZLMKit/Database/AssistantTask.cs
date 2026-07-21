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

[Table("assistant_tasks")]
public class AssistantTask
{
    [Column("task_id"), PrimaryKey, AutoIncrement]
    public int TaskId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, COMPLETED, PAUSED

    [Column("objective")]
    public string Objective { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; }

    // Store as JSON strings, since SQLite does not support arrays directly.
    [Column("completed_steps_json")]
    public string CompletedStepsJson { get; set; } = "[]";

    [Column("next_steps_json")]
    public string NextStepsJson { get; set; } = "[]";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
