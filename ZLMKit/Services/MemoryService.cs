/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BSLib;
using SmartComponents.LocalEmbeddings;
using ZLMKit.Database;
using ZLMKit.MCP;

namespace ZLMKit.Services;


/// <summary>
/// Helper class for RAG (Retrieval-Augmented Generation).
/// </summary>
internal class MemoryService
{
    private readonly int _tokenThresholdChars = 5000; // Trigger threshold in characters (~4-5k characters)
    private static readonly NumberFormatInfo fNumberFormat;
    private static readonly Dictionary<string, (EmbeddingF32 Vector, DateTime Timestamp)> fEmbeddingsCache = new();

    public MemoryService()
    {
    }

    static MemoryService()
    {
        fNumberFormat = new NumberFormatInfo();
        fNumberFormat.NumberDecimalSeparator = ".";
    }

    private static readonly TimeSpan CacheTTL = TimeSpan.FromHours(2);
    private static readonly int MaxCacheSize = 500;

    internal static EmbeddingF32 GetCachedEmbedding(string text)
    {
        if (fEmbeddingsCache.TryGetValue(text, out var cached) && DateTime.UtcNow - cached.Timestamp < CacheTTL)
            return cached.Vector;

        var embedding = Embed(text);

        // LRU overflow eviction
        if (fEmbeddingsCache.Count >= MaxCacheSize) {
            var oldest = fEmbeddingsCache.OrderBy(kv => kv.Value.Timestamp).First().Key;
            fEmbeddingsCache.Remove(oldest);
        }

        fEmbeddingsCache[text] = (embedding, DateTime.UtcNow);
        return embedding;
    }

    public static void ClearEmbeddingsCache()
    {
        fEmbeddingsCache.Clear();
    }

    #region Utilities

    private static LocalEmbedder fEmbedder = null;

    public static EmbeddingF32 Embed(string inputText, int maximumTokens = 512)
    {
        if (fEmbedder == null) {
            fEmbedder = new LocalEmbedder();
        }
        var embedding = fEmbedder.Embed(inputText, maximumTokens)/*.Values.ToArray()*/;
        return embedding;
    }

    public static string SetVector(float[] values)
    {
        var strValues = values.Select(x => x.ToString(fNumberFormat));
        return string.Join(';', strValues);
    }

    public static float[] GetVector(string embedding)
    {
        var values = embedding.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var vector = new float[values.Length];
        for (int i = 0; i < values.Length; i++) {
            vector[i] = float.Parse(values[i], fNumberFormat);
        }
        return vector;
    }

    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must be the same length");

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;
        for (int i = 0; i < vectorA.Length; i++) {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }
        float divisor = (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
        return divisor == 0 ? 0 : dotProduct / divisor;
    }

    public static string[] DeserializeJsonList(string json)
    {
        try {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        } catch {
            return Array.Empty<string>();
        }
    }

    #endregion

    #region Context

    /// <summary>
    /// Aggregates all memory layers into a single text block for injection into the LM context
    /// </summary>
    public string GetInjectedContext(string sessionId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ASSISTANT OFFLINE MEMORY (HARD CONTEXT) ===");
        AssistantSummary summary = LLMDatabase.GetSummary(sessionId);

        sb.AppendLine();

        var preferences = LLMDatabase.GetUserPreferences();
        sb.AppendLine("[USER PROFILE AND PREFERENCES]:");
        if (preferences.Count > 0) {
            foreach (var pref in preferences) {
                sb.AppendLine($"- {pref.PrefKey}: {pref.PrefValue} (Confidence: {pref.ConfidenceScore:F1})");
            }
        } else {
            sb.AppendLine("- Profile is empty. Specific preferences have not yet been identified.");
        }
        sb.AppendLine();

        var activeTasks = LLMDatabase.GetActiveTasks();
        sb.AppendLine("[ACTIVE ASSISTANT TASKS IN PROGRESS]:");
        if (activeTasks.Count > 0) {
            foreach (var task in activeTasks) {
                sb.AppendLine($"* TASK ID: {task.TaskId}");
                sb.AppendLine($"  Objective: {task.Objective}");
                sb.AppendLine($"  Description: {task.Description}");

                // Format JSON arrays back into readable lists
                var completedSteps = DeserializeJsonList(task.CompletedStepsJson);
                sb.AppendLine($"  Completed steps: {(completedSteps.Length > 0 ? string.Join(", ", completedSteps) : "no steps completed yet")}");

                var nextSteps = DeserializeJsonList(task.NextStepsJson);
                sb.AppendLine($"  Planned steps: {(nextSteps.Length > 0 ? string.Join(" -> ", nextSteps) : "not defined")}");
            }
        } else {
            sb.AppendLine("- No active tasks. Any new complex user request should initiate task creation.");
        }
        sb.AppendLine("=================================================");

        return sb.ToString();
    }

    /// <summary>
    /// Adds new dialogue turns to the current session and compresses them if necessary.
    /// </summary>
    public void AppendAndOptimizeContext(IMCPServer mcpServer, string sessionId, string newUserMessage, string assistantResponse)
    {
        // 1. Get current record from DB
        var summary = LLMDatabase.GetSummary(sessionId);

        if (summary == null) {
            summary = new AssistantSummary {
                SessionId = sessionId,
                GlobalSummary = "Start of new genealogical research.",
                SessionSummary = $"User: {newUserMessage}\nAssistant: {assistantResponse}\n",
                UpdatedAt = DateTime.UtcNow
            };
            LLMDatabase.InsertSummary(summary);
            return;
        }

        // 2. Append new turns to current context
        summary.SessionSummary += $"User: {newUserMessage}\nAssistant: {assistantResponse}\n";
        summary.UpdatedAt = DateTime.UtcNow;

        // 3. Check if context compression is needed (length-based estimate for simplicity in offline solutions)
        if (summary.SessionSummary.Length > _tokenThresholdChars) {
            // Launch summarization. To avoid making the user wait for MCP response,
            // in a real server this could be run via Task.Run without await (fire-and-forget),
            // but with mandatory error handling inside.
            _ = Task.Run(async () => {
                try {
                    await CompressContextAsync(mcpServer, summary);
                } catch (Exception ex) {
                    // Your logger should go here (Console.Error.WriteLine for MCP)
                    //Console.Error.WriteLine($"❌ Background context compression failed: {ex.Message}");
                }
            });
        } else {
            LLMDatabase.UpdateSummary(summary);
        }
    }

    /// <summary>
    /// Calls the local model to transfer the current session into global memory
    /// </summary>
    private async Task CompressContextAsync(IMCPServer mcpServer, AssistantSummary summary)
    {
        // Formulate a strict system prompt for a weaker model to prevent hallucinations
        string compressPrompt = $@"You are the background memory compression module for a genealogical assistant. 
Your task is to merge the old global summary and new dialogue turns into ONE concise, fact-dense paragraph.
Ignore pleasantries, greetings, and filler. Write strictly facts, names, dates, and social estates.

OLD GLOBAL SUMMARY:
{summary.GlobalSummary}

NEW DIALOGUE TURNS FROM CURRENT SESSION:
{summary.SessionSummary}

Output the new merged global summary in English. It must contain ALL key chronological information. Do not write anything except the summary.";

        var lmClient = mcpServer.Chat;
        if (lmClient == null) return;

        // Send request to local model (minimal temperature for factual accuracy)
        var newGlobalSummary = await lmClient.SendMessageSingleAsync("user", compressPrompt, 0.1f);

        if (!string.IsNullOrEmpty(newGlobalSummary)) {
            // Update state in DB: 
            // Clear current session (or keep last 2 lines), and consolidate global memory.
            summary.GlobalSummary = newGlobalSummary;
            summary.SessionSummary = "Context cleared after archival. Dialogue continues from this point.\n";
            summary.UpdatedAt = DateTime.UtcNow;

            LLMDatabase.UpdateSummary(summary);
        }
    }

    #endregion

    #region Profile

    /// <summary>
    /// Adds or updates a user setting/preference in upsert mode.
    /// </summary>
    public bool SetPreference(string key, string value, double confidenceScore = 1.0)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        // Normalize key to lowercase to avoid duplicates due to case variations from small models
        string normalizedKey = key.Trim().ToLowerInvariant();
        var existingPref = LLMDatabase.GetPreference(normalizedKey);

        if (existingPref != null) {
            // If value is the same, just update date and confidence if it's higher
            existingPref.PrefValue = value.Trim();
            existingPref.ConfidenceScore = Math.Max(existingPref.ConfidenceScore, confidenceScore);
            existingPref.UpdatedAt = DateTime.UtcNow;
            LLMDatabase.UpdatePreference(existingPref);
        } else {
            var newPref = new UserPreference {
                PrefKey = normalizedKey,
                PrefValue = value.Trim(),
                ConfidenceScore = confidenceScore,
                UpdatedAt = DateTime.UtcNow
            };
            LLMDatabase.InsertPreference(newPref);
        }

        return true;
    }

    /// <summary>
    /// Returns all preferences as a convenient dictionary.
    /// </summary>
    public Dictionary<string, string> GetAllPreferences()
    {
        var list = LLMDatabase.GetUserPreferences();
        var result = new Dictionary<string, string>();

        foreach (var item in list) {
            result[item.PrefKey] = item.PrefValue;
        }

        return result;
    }

    /// <summary>
    /// Removes a specific key from the profile if the preference is no longer relevant.
    /// </summary>
    public bool DeletePreference(string key)
    {
        string normalizedKey = key.Trim().ToLowerInvariant();
        var existing = LLMDatabase.GetPreference(normalizedKey);

        if (existing == null) return false;
        LLMDatabase.DeletePreference(existing);
        return true;
    }

    #endregion

    #region Task Board

    /// <summary>
    /// Creates a new assistant task.
    /// </summary>
    public int CreateTask(string objective, string description)
    {
        var task = new AssistantTask {
            Objective = objective?.Trim() ?? string.Empty,
            Description = description?.Trim() ?? string.Empty,
            Status = "ACTIVE",
            CompletedStepsJson = "[]",
            NextStepsJson = "[]",
            UpdatedAt = DateTime.UtcNow
        };

        LLMDatabase.InsertTask(task);
        return task.TaskId; // sqlite-net automatically populates ID after insert
    }

    /// <summary>
    /// Adds a completed step and/or overwrites the plan for next steps.
    /// </summary>
    public bool UpdateTaskProgress(int taskId, string newCompletedStep, string[] newNextSteps)
    {
        var task = LLMDatabase.GetTask(taskId);

        if (task == null) return false;

        // 1. Update completed steps (if a new one is provided)
        if (!string.IsNullOrWhiteSpace(newCompletedStep)) {
            var steps = JsonSerializer.Deserialize<List<string>>(task.CompletedStepsJson) ?? new List<string>();
            string normalizedStep = newCompletedStep.Trim();

            if (!steps.Contains(normalizedStep)) {
                steps.Add(normalizedStep);
                task.CompletedStepsJson = JsonSerializer.Serialize(steps);
            }
        }

        // 2. Overwrite next steps (plan is always dynamic)
        if (newNextSteps != null) {
            task.NextStepsJson = JsonSerializer.Serialize(newNextSteps);
        }

        task.UpdatedAt = DateTime.UtcNow;
        LLMDatabase.UpdateTask(task);
        return true;
    }

    /// <summary>
    /// Changes task status (COMPLETED, PAUSED, ACTIVE)
    /// </summary>
    public bool ChangeTaskStatus(int taskId, string newStatus)
    {
        var task = LLMDatabase.GetTask(taskId);

        if (task == null) return false;

        string status = newStatus?.Trim().ToUpperInvariant() ?? "ACTIVE";
        if (status == "ACTIVE" || status == "COMPLETED" || status == "PAUSED") {
            task.Status = status;
            LLMDatabase.UpdateTask(task);
            return true;
        }

        return false;
    }

    #endregion

    #region Memory

    private record EmbedResult(int EntityId, float AggregateScore);

    private sealed class SearchResult
    {
        public QMemoryEntity Entity { get; }

        public double Score { get; set; }

        public SearchResult(QMemoryEntity entity, double score)
        {
            Entity = entity;
            Score = score;
        }
    }

    /// <summary>
    /// Pure semantic search using embedding similarity.
    /// </summary>
    private static List<EmbedResult> SearchEntitiesSemantic(string query, int topK = 10)
    {
        var inputVector = Embed(query);
        var entries = LLMDatabase.GetObservationEmbeddings();

        var bestMatches = entries
            // 1. Считаем скор для каждого отдельного наблюдения
            .Select(mo => new {
                mo.EntityId,
                Score = inputVector.Similarity(new EmbeddingF32(mo.Embedding))
            })
            // 2. Группируем наблюдения по сущностям (EntityId)
            .GroupBy(x => x.EntityId)
            // 3. Агрегируем скоры внутри каждой сущности
            .Select(g => new EmbedResult(
                g.Key,
                // Формула Noisy-OR: 1 - ( (1-s1) * (1-s2) * ... )
                //1.0f - g.Aggregate(1.0f, (acc, item) => acc * (1.0f - item.Score))
                // Формула Max Pooling:
                g.Max(x => x.Score)
            ))
            // 4. Сортируем сущности по итоговому весу и берем topK
            .OrderByDescending(x => x.AggregateScore)
            .Take(topK)
            .ToList();

        return bestMatches;
    }

    public static string SearchMemory(IRuntimeContext context, string query, int topK = 10)
    {
        if (!context.FTSEnabled) {
            // Original embedding-based search
            var entries = SearchEntitiesSemantic(query, topK);
            var bestMatches = entries.Select(entry => new SearchResult(LLMDatabase.GetEntity(entry.EntityId), entry.AggregateScore)).ToList();

            // Forming a context for the MCP server
            string examples = OutputResults(bestMatches);
            return examples;
        } else {
            // Perform hybrid search with FTS and embeddings using RRF
            return SearchEntitiesHybrid(context, query, topK);
        }
    }

    /// <summary>
    /// Combines vector and keyword signals via Reciprocal Rank Fusion (RRF).
    /// </summary>
    private static string SearchEntitiesHybrid(IRuntimeContext context, string query, int topK = 10)
    {
        // standard RRF constant
        const double k = 60.0;

        // Get embedding-based matches from observations
        var entries = SearchEntitiesSemantic(query, topK);
        var semanticMatches = entries.Select(entry => new { Entry = LLMDatabase.GetEntity(entry.EntityId), Score = entry.AggregateScore }).ToList();

        // Get FTS matches from observations
        var ftsMatches = LLMDatabase.SearchMemoryEntitiesFTS(query, topK * 2);

        // Apply Reciprocal Rank Fusion (RRF)
        var rrfResults = new Dictionary<int, SearchResult>();

        // Process semantic matches
        for (int i = 0; i < semanticMatches.Count; i++) {
            var match = semanticMatches[i];
            double rrfScore = 1.0 / (k + (i + 1)); // k = 60 as in the paper

            if (rrfResults.TryGetValue(match.Entry.Id, out var existing)) {
                existing.Score += rrfScore;
            } else {
                rrfResults[match.Entry.Id] = new SearchResult(match.Entry, rrfScore);
            }
        }

        // Process FTS matches (convert observations to entities for compatibility)
        for (int i = 0; i < ftsMatches.Count; i++) {
            var match = ftsMatches[i];
            double rrfScore = 1.0 / (k + (i + 1)); // k = 60 as in the paper

            if (rrfResults.TryGetValue(match.Id, out var existing)) {
                existing.Score += rrfScore;
            } else {
                rrfResults[match.Id] = new SearchResult(match, rrfScore);
            }
        }

        // Sort by RRF score and take topK
        var bestMatches = rrfResults.Values
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        // Forming a context for the MCP server
        string examples = OutputResults(bestMatches);
        return examples;
    }

    /// <summary>
    /// Forming a context for the MCP server.
    /// </summary>
    private static string OutputResults(List<SearchResult> bestMatches)
    {
        string examples = $@"<memory>
<instruction>
This is data from memory. Use it in your answer and mention that you remembered it.
</instruction>

{string.Join("\n\n", bestMatches.Select((m) => $@"
<fact id=""{m.Entity.Id}"" score=""{m.Score:F3}"">
{m.Entity.Content}
</fact>"))}

<guidance>
{(bestMatches.Average(m => m.Score) < 0.5
    ? "⚠️ Facts with low similarity were found. Pay particular attention to deviations in structure."
    : "✅ The facts are relevant. Follow their structure.")}
</guidance>
</memory>";

        return examples;
    }

    /// <summary>
    /// Adds or updates a node in the knowledge graph
    /// </summary>
    public int AddOrUpdateEntity(IRuntimeContext context, int id, string name, string type, string observationContent = "")
    {
        try {
            name = name.Trim();
            type = type.Trim().ToUpperInvariant();

            MemoryEntity entity = (id < 0) ? null : LLMDatabase.GetEntity(id);
            if (entity != null) {
                if (!string.IsNullOrEmpty(name)) entity.Name = name;
                if (!string.IsNullOrEmpty(type)) entity.Type = type;
                entity.UpdatedAt = DateTime.UtcNow;

                LLMDatabase.UpdateEntity(entity);
            } else {
                id = LLMDatabase.GetEntityId(name);
                if (id != 0)
                    return -1;

                entity = new MemoryEntity {
                    Name = name,
                    Type = type,
                    UpdatedAt = DateTime.UtcNow
                };

                // SQLite-net automatically sets the PK value in the object when inserting
                LLMDatabase.InsertEntity(entity);
            }

            // Also add an observation for this entity
            if (!string.IsNullOrEmpty(observationContent))
                AddObservationForEntity(context, entity.Id, observationContent);

            return entity.Id;
        } catch (Exception ex) {
            context.Get<ILogger>()?.WriteError("AddOrUpdateEntity()", ex);
            return -2;
        }
    }

    /// <summary>
    /// Adds an observation for an entity
    /// </summary>
    public void AddObservationForEntity(IRuntimeContext context, int entityId, string content)
    {
        try {
            var observation = new MemoryObservation {
                EntityId = entityId,
                Content = content.Trim()
            };

            var embedding = Embed(observation.Content);
            observation.Embedding = embedding.Buffer.ToArray();
            observation.CreatedAt = DateTime.UtcNow;

            LLMDatabase.InsertObservation(observation);
        } catch (Exception ex) {
            context.Get<ILogger>()?.WriteError("AddObservationForEntity()", ex);
        }
    }

    /// <summary>
    /// Creates a directed relationship between two graph nodes
    /// </summary>
    public void AddRelation(int sourceId, string predicate, int targetId, string notes = "")
    {
        var pred = predicate.Trim().ToUpperInvariant();

        // Check for duplicate relationships to prevent graph clutter
        var existing = LLMDatabase.GetRelation(sourceId, targetId, pred);

        if (existing == null) {
            LLMDatabase.InsertRelation(new MemoryRelation {
                SourceEntityId = sourceId,
                Predicate = pred,
                TargetEntityId = targetId,
                ContextNotes = notes.Trim()
            });
        }
    }

    /// <summary>
    /// Extracts the ego-network (entity and all its first-order connections) for the local LM
    /// </summary>
    public string GetLocalSubGraphAsText(int entityId)
    {
        var mainEntity = LLMDatabase.GetEntity(entityId);
        if (mainEntity == null) return $"❌ Entity with ID '{entityId}' not found in knowledge base.";

        var sb = new StringBuilder();
        sb.AppendLine($"--- LOCAL KNOWLEDGE SUBGRAPH: {mainEntity.Name} ({mainEntity.Type}) ---");
        if (!string.IsNullOrEmpty(mainEntity.Content)) sb.AppendLine($"Description: {mainEntity.Content}");
        sb.AppendLine("RELATIONS AND CONTEXTUAL LANDSCAPE:");

        // Outgoing relations (From this node)
        var outgoing = LLMDatabase.GetRelationBySource(entityId);
        foreach (var rel in outgoing) {
            var target = LLMDatabase.GetEntity(rel.TargetEntityId);
            string targetName = target != null ? $"{target.Name} [{target.Type}]" : rel.TargetEntityId.ToString();
            string notes = string.IsNullOrEmpty(rel.ContextNotes) ? "" : $" ({rel.ContextNotes})";
            sb.AppendLine($"  => [{mainEntity.Name}] --({rel.Predicate})--> [{targetName}]{notes}");
        }

        // Incoming relations (To this node)
        var incoming = LLMDatabase.GetRelationByTarget(entityId);
        foreach (var rel in incoming) {
            var source = LLMDatabase.GetEntity(rel.SourceEntityId);
            string sourceName = source != null ? $"{source.Name} [{source.Type}]" : rel.SourceEntityId.ToString();
            string notes = string.IsNullOrEmpty(rel.ContextNotes) ? "" : $" ({rel.ContextNotes})";
            sb.AppendLine($"  <= [{sourceName}] --({rel.Predicate})--> [{mainEntity.Name}]{notes}");
        }

        return sb.ToString();
    }

    #endregion
}
