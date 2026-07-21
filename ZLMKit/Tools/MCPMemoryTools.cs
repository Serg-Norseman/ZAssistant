/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using ZLMKit.Database;
using ZLMKit.MCP;
using ZLMKit.Protocols;
using ZLMKit.Services;

namespace ZLMKit.Tools;


internal class SearchMemoryTool : BaseTool
{
    public SearchMemoryTool() : base("search_memory") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Entities search in the long-term memory using semantic similarity. Returns unique identifiers, entity text (aggregated observations), and similarity.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["query"] = new MCPToolProperty { Type = "string", Description = "The search query for finding relevant memories" },
                    ["top_k"] = new MCPToolProperty { Type = "integer", Description = "Maximum number of results to return", Default = 5 },

                    //["filter_tags"] = new MCPToolProperty { Type = "string", Description = "Optional tags to filter memories" }
                },
                Required = new List<string> { "query" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string query = MCPHelper.GetRequiredStr(args, "query");
        int topK = MCPHelper.GetOptionalInt(args, "top_k", 5);

        var results = MemoryService.SearchMemory(context, query, topK);

        return MCPContent.CreateSimpleContent(results);
    }
}


internal class GetKnowledgeSubgraphTool : BaseTool
{
    public GetKnowledgeSubgraphTool() : base("get_knowledge_subgraph") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Retrieve the local knowledge subgraph centered around a specific entity. List of entities in radius 1 for incoming and outgoing relations.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["entity_id"] = new MCPToolProperty { Type = "integer", Description = "The unique identifier of the entity" }
                },
                Required = new List<string> { "entity_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int entityId = MCPHelper.GetRequiredInt(args, "entity_id");

        var service = new MemoryService();
        string result = service.GetLocalSubGraphAsText(entityId);

        return MCPContent.CreateSimpleContent(result);
    }
}


internal class UpsertMemoryEntityTool : BaseTool
{
    public UpsertMemoryEntityTool() : base("upsert_memory_entity") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Add a new entity or update existing in the long-term memory (knowledge graph). Observations of facts are always only added to this function, they are immutable and non-replaceable. Returns unique identifier for the entity.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["entity_id"] = new MCPToolProperty { Type = "integer", Description = "Unique identifier for the entity being updated; not required when adding" },
                    ["name"] = new MCPToolProperty { Type = "string", Description = "Human-readable name of the entity" },
                    ["type"] = new MCPToolProperty { Type = "string", Description = "Entity type/category (e.g., 'person', 'location', 'event', 'conversation', 'document', etc.)" },
                    ["content"] = new MCPToolProperty { Type = "string", Description = "Human-readable text containing a detailed description or content of the fact of observation of an entity" }

                    //["tags"] = new MCPToolProperty { Type = "string", Description = "Optional tags to categorize the fact" },
                    //["confidence"] = new MCPToolProperty { Type = "number", Description = "Confidence score for the memory (0.0 to 1.0)" },
                },
                Required = new List<string>() { }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int entityId = MCPHelper.GetOptionalInt(args, "entity_id", -1);
        string name = MCPHelper.GetOptionalStr(args, "name", "");
        string type = MCPHelper.GetOptionalStr(args, "type", "");
        string content = MCPHelper.GetOptionalStr(args, "content", "");

        var service = new MemoryService();
        int resultId = service.AddOrUpdateEntity(context, entityId, name, type, content);

        if (resultId <= 0) {
            string reason = (resultId == -1) ? "An entity with this name exists." : "Internal error.";

            return MCPContent.CreateSimpleContent($"❌ Entity '{name}' [{type}] is not recorded. {reason}");
        } else {
            return MCPContent.CreateSimpleContent($"✅ Entity '{name}' [{type}, ID: {resultId}] successfully recorded in the long-term memory (knowledge graph).");
        }
    }
}


internal class AddMemoryRelationTool : BaseTool
{
    public AddMemoryRelationTool() : base("add_memory_relation") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Create a semantic relationship between two knowledge entities with contextual notes",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["source_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the source entity" },
                    ["predicate"] = new MCPToolProperty { Type = "string", Description = "Relationship type (e.g., 'born_in', 'parent_of', 'worked_at')" },
                    ["target_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the target entity" },
                    ["context_notes"] = new MCPToolProperty { Type = "string", Description = "Additional context or evidence for this relationship" }
                },
                Required = new List<string> { "source_id", "predicate", "target_id", "context_notes" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int sourceId = MCPHelper.GetRequiredInt(args, "source_id");
        string predicate = MCPHelper.GetRequiredStr(args, "predicate");
        int targetId = MCPHelper.GetRequiredInt(args, "target_id");
        string contextNotes = MCPHelper.GetRequiredStr(args, "context_notes");

        var service = new MemoryService();
        service.AddRelation(sourceId, predicate, targetId, contextNotes);

        return MCPContent.CreateSimpleContent($"✅ Relationship successfully created: {sourceId} --({predicate})--> {targetId}.");
    }
}


internal class GetMemoryEntityTool : BaseTool
{
    public GetMemoryEntityTool() : base("get_memory_entity") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Retrieve information about a specific entity from the long-term memory (knowledge graph). The name, type, identifier, and full text of observations are returned. No relations.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["entity_id"] = new MCPToolProperty { Type = "integer", Description = "Unique identifier for the entity" }
                },
                Required = new List<string>() { "entity_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int entityId = MCPHelper.GetRequiredInt(args, "entity_id");

        var entity = LLMDatabase.GetEntity(entityId);
        if (entity == null)
            return MCPContent.CreateSimpleContent($"❌ Entity with ID '{entityId}' not found in the knowledge graph.");

        var sb = new StringBuilder();
        sb.AppendLine($"Entity: {entity.Name} [{entity.Type}]");
        sb.AppendLine($"ID: {entity.Id}");
        sb.AppendLine($"Content:");
        sb.AppendLine(entity.Content);

        return MCPContent.CreateSimpleContent(sb.ToString());
    }
}


internal class DeleteMemoryEntityTool : BaseTool
{
    public DeleteMemoryEntityTool() : base("delete_memory_entity") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Delete an entity from the long-term memory (knowledge graph), with relations to other entities, but without cascade.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["entity_id"] = new MCPToolProperty { Type = "integer", Description = "Unique identifier for the entity to delete" }
                },
                Required = new List<string>() { "entity_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int entityId = MCPHelper.GetRequiredInt(args, "entity_id");

        var entity = LLMDatabase.GetEntity(entityId);
        if (entity == null) {
            return MCPContent.CreateSimpleContent($"❌ Entity with ID '{entityId}' not found in the knowledge graph.");
        }

        // Delete all relations associated with this entity
        var outgoingRelations = LLMDatabase.GetRelationBySource(entityId);
        var incomingRelations = LLMDatabase.GetRelationByTarget(entityId);
        var allRelations = outgoingRelations.Concat(incomingRelations).ToList();

        foreach (var relation in allRelations) {
            LLMDatabase.DeleteRelation(relation);
        }

        LLMDatabase.DeleteAllObservations(entityId);

        // Delete the entity itself
        LLMDatabase.DeleteEntity(entity);

        return MCPContent.CreateSimpleContent($"✅ Entity '{entity.Name}' [{entity.Type}] successfully deleted from the knowledge graph along with {allRelations.Count} associated relations.");
    }
}


internal class MergeMemoryEntitiesTool : BaseTool
{
    public MergeMemoryEntitiesTool() : base("merge_memory_entities") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Merge two entities in the knowledge graph - only moving all relations to the target entity and deleting the source entity (if the target entity exists). To merge data of entity itself, read from source and write to target.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["source_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the source entity to be merged and deleted" },
                    ["target_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the target entity to which all data will be merged" }
                },
                Required = new List<string> { "source_id", "target_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int sourceId = MCPHelper.GetRequiredInt(args, "source_id");
        int targetId = MCPHelper.GetRequiredInt(args, "target_id");

        var sourceEntity = LLMDatabase.GetEntity(sourceId);
        if (sourceEntity == null) {
            return MCPContent.CreateSimpleContent($"❌ Source entity with ID '{sourceId}' not found in the knowledge graph.");
        }

        var targetEntity = LLMDatabase.GetEntity(targetId);
        if (targetEntity == null) {
            return MCPContent.CreateSimpleContent($"❌ Target entity with ID '{targetId}' not found in the knowledge graph.");
        }

        // Update all relations pointing to source entity to point to target entity
        var incomingRelations = LLMDatabase.GetRelationByTarget(sourceId);
        int updatedIncoming = 0;
        foreach (var relation in incomingRelations) {
            relation.TargetEntityId = targetId;
            LLMDatabase.UpdateRelation(relation);
            updatedIncoming++;
        }

        var outgoingRelations = LLMDatabase.GetRelationBySource(sourceId);
        int updatedOutgoing = 0;
        foreach (var relation in outgoingRelations) {
            // Check if a similar relation already exists to avoid duplicates
            var existingRelation = LLMDatabase.GetRelation(relation.SourceEntityId, targetId, relation.Predicate);
            if (existingRelation == null) {
                relation.SourceEntityId = targetId;
                LLMDatabase.UpdateRelation(relation);
                updatedOutgoing++;
            } else {
                // If relation already exists, we might want to merge context notes
                if (!string.IsNullOrEmpty(relation.ContextNotes) && !existingRelation.ContextNotes.Contains(relation.ContextNotes)) {
                    existingRelation.ContextNotes += "; " + relation.ContextNotes;
                    LLMDatabase.UpdateRelation(existingRelation);
                }
                // Delete the duplicate relation
                LLMDatabase.DeleteRelation(relation);
            }
        }

        // Delete the source entity
        LLMDatabase.DeleteEntity(sourceEntity);

        return MCPContent.CreateSimpleContent($"✅ Entities successfully merged: '{sourceEntity.Name}' -> '{targetEntity.Name}'. " +
            $"Updated {updatedIncoming} incoming and {updatedOutgoing} outgoing relations. Source entity deleted.");
    }
}


internal class ListMemoryEntitiesTool : BaseTool
{
    public ListMemoryEntitiesTool() : base("list_memory_entities") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "List all entities in the long-term memory (knowledge graph) in a paginated table format (20 rows per page). Returns name, type, identifier.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["type"] = new MCPToolProperty { Type = "string", Description = "Optional entity type/category (e.g., 'person', 'location', 'event', 'conversation', 'document', etc.)" },
                    ["page"] = new MCPToolProperty { Type = "integer", Description = "Optional page number for pagination (default: 1)", Default = 1 }
                },
                Required = new List<string>() { }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string type = MCPHelper.GetOptionalStr(args, "type", null);

        // Get all entities from the database
        var entities = LLMDatabase.GetAllEntities(type);
        int recordCount = entities.Count;

        // the page is defined in this method
        return MCPHelper.PageableTable(
            "Memory Entities",
            args,
            recordCount,
            (index) => {
                if (index == -1) // Header row
                    return $"| ID | Name | Type |\n|----|------|------|";

                var entity = entities[index];
                return $"| {entity.Id} | {entity.Name} | {entity.Type} |";
            });
    }
}


internal class GetMemoryRelationTool : BaseTool
{
    public GetMemoryRelationTool() : base("get_memory_relation") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Retrieve information about a specific relation between entities in the knowledge graph.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["relation_id"] = new MCPToolProperty { Type = "integer", Description = "Unique identifier for the relation" }
                },
                Required = new List<string>() { "relation_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int relationId = MCPHelper.GetRequiredInt(args, "relation_id");

        var relation = LLMDatabase.GetRelationById(relationId);
        if (relation == null)
            return MCPContent.CreateSimpleContent($"❌ Relation with ID '{relationId}' not found in the knowledge graph.");

        var sb = new StringBuilder();
        sb.AppendLine($"Relation ID: {relation.RelationId}");
        sb.AppendLine($"From entity ID: {relation.SourceEntityId}");
        sb.AppendLine($"Predicate: {relation.Predicate}");
        sb.AppendLine($"To entity ID: {relation.TargetEntityId}");
        sb.AppendLine($"Context notes: {relation.ContextNotes}");

        return MCPContent.CreateSimpleContent(sb.ToString());
    }
}


internal class UpdateMemoryRelationTool : BaseTool
{
    public UpdateMemoryRelationTool() : base("update_memory_relation") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Update an existing relation between entities in the knowledge graph.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["relation_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the relation to update" },
                    ["predicate"] = new MCPToolProperty { Type = "string", Description = "New predicate for the relation" },
                    ["context_notes"] = new MCPToolProperty { Type = "string", Description = "New context notes for the relation" }
                },
                Required = new List<string>() { "relation_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int relationId = MCPHelper.GetRequiredInt(args, "relation_id");
        string predicate = MCPHelper.GetOptionalStr(args, "predicate", null);
        string contextNotes = MCPHelper.GetOptionalStr(args, "context_notes", null);

        var relation = LLMDatabase.GetRelationById(relationId);
        if (relation == null)
            return MCPContent.CreateSimpleContent($"❌ Relation with ID '{relationId}' not found in the knowledge graph.");

        // Update only provided fields
        if (!string.IsNullOrEmpty(predicate))
            relation.Predicate = predicate.Trim().ToUpperInvariant();

        if (contextNotes != null) // Allow empty strings
            relation.ContextNotes = contextNotes.Trim();

        LLMDatabase.UpdateRelation(relation);

        return MCPContent.CreateSimpleContent($"✅ Relation ID {relationId} successfully updated.");
    }
}


internal class DeleteMemoryRelationTool : BaseTool
{
    public DeleteMemoryRelationTool() : base("delete_memory_relation") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Delete a relation between entities in the knowledge graph.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["relation_id"] = new MCPToolProperty { Type = "integer", Description = "ID of the relation to delete" }
                },
                Required = new List<string>() { "relation_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
                DestructiveHint = true,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int relationId = MCPHelper.GetRequiredInt(args, "relation_id");

        var relation = LLMDatabase.GetRelationById(relationId);
        if (relation == null)
            return MCPContent.CreateSimpleContent($"❌ Relation with ID '{relationId}' not found in the knowledge graph.");

        LLMDatabase.DeleteRelation(relation);

        return MCPContent.CreateSimpleContent($"✅ Relation ID {relationId} successfully deleted from the knowledge graph.");
    }
}
