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


[Table("memory_entities")]
public class MemoryEntity
{
    [Column("id"), PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } // Readable name: "деревня Ковалево", "Суслов Иван Петрович"

    [Column("type")]
    public string Type { get; set; } // Type: PERSON, LOCATION, ARCHIVE, HISTORICAL_EVENT, SOCIAL_CLASS

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}


public class QMemoryEntity : MemoryEntity
{
    [Column("content")]
    public string Content { get; set; } // Generated dynamically, not stored
}


[Table("memory_observations")]
public class MemoryObservation
{
    [Column("id"), PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Column("entity_id")]
    public int EntityId { get; set; } // Reference to memory_entities.id

    [Column("content")]
    public string Content { get; set; }

    [Column("embedding")]
    public byte[] Embedding { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("confidence_score")]
    public float ConfidenceScore { get; set; } = 1.0f;
}


[Table("memory_relations")]
public class MemoryRelation
{
    [Column("relation_id"), PrimaryKey, AutoIncrement]
    public int RelationId { get; set; }

    [Column("source_entity_id"), Indexed]
    public int SourceEntityId { get; set; }

    [Column("predicate")]
    public string Predicate { get; set; } // Relation type: BORN_IN, BELONGED_TO_PARISH, STORED_IN, MARRIED_TO, MIGRATED_TO

    [Column("target_entity_id"), Indexed]
    public int TargetEntityId { get; set; }

    [Column("context_notes")]
    public string ContextNotes { get; set; } = string.Empty; // Clarification (for example, the years of validity of this connection: "from 1890 to 1915")
}
