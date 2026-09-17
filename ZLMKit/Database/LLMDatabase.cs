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
using System.Linq;
using SQLite;

namespace ZLMKit.Database;


public static class LLMDatabase
{
    private sealed class DBPatch
    {
        public int ReqVer;
        public string Sql;

        public DBPatch(int reqVer, string sql)
        {
            ReqVer = reqVer;
            Sql = sql;
        }
    }


    // Current database version
    private const int CURRENT_DB_VERSION = 2;

    // List of SQL commands to create tables with version indication when they were added
    private static readonly Dictionary<int, string[]> MigrationSQL = new Dictionary<int, string[]>
    {
        { 1, new string[] {
            @"CREATE TABLE IF NOT EXISTS [assistant_summary] (
                [session_id] TEXT NOT NULL PRIMARY KEY,
                [global_summary] TEXT,
                [session_summary] TEXT,
                [updated_at] TEXT
            )",

            @"CREATE TABLE IF NOT EXISTS [assistant_tasks] (
                [task_id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                [status] TEXT,
                [objective] TEXT,
                [description] TEXT,
                [completed_steps_json] TEXT,
                [next_steps_json] TEXT,
                [updated_at] TEXT
            )",

            @"CREATE TABLE IF NOT EXISTS [memory_entities] (
                [id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                [name] TEXT,
                [type] TEXT,
                [updated_at] TEXT
            )",
            @"CREATE UNIQUE INDEX IF NOT EXISTS idx_memory_entities_name ON memory_entities(name)",
            @"CREATE INDEX IF NOT EXISTS idx_memory_entities_type ON memory_entities(type)",

            @"CREATE TABLE IF NOT EXISTS [memory_observations] (
                [id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                [entity_id] INTEGER NOT NULL,
                [content] TEXT,
                [embedding] BLOB,
                [created_at] TEXT,
                [confidence_score] REAL DEFAULT 1.0
            )",

            @"CREATE TABLE IF NOT EXISTS [memory_relations] (
                [relation_id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                [source_entity_id] INTEGER,
                [predicate] TEXT,
                [target_entity_id] INTEGER,
                [context_notes] TEXT
            )",
            @"CREATE INDEX IF NOT EXISTS idx_memory_relations_source_entity_id ON memory_relations(source_entity_id)",
            @"CREATE INDEX IF NOT EXISTS idx_memory_relations_target_entity_id ON memory_relations(target_entity_id)",

            @"CREATE TABLE IF NOT EXISTS [user_preferences] (
                [pref_key] TEXT NOT NULL PRIMARY KEY,
                [pref_value] TEXT,
                [confidence_score] REAL,
                [updated_at] TEXT
            )",

            // Add FTS support for memory_observations table
            @"CREATE VIRTUAL TABLE IF NOT EXISTS memory_observations_fts USING fts5(id, content)",

            // Populate FTS table with existing data
            @"INSERT INTO memory_observations_fts(id, content) SELECT id, content FROM memory_observations",

            // Triggers to keep FTS table in sync with memory_observations table
            @"CREATE TRIGGER IF NOT EXISTS memory_observations_ai AFTER INSERT ON memory_observations BEGIN
                INSERT INTO memory_observations_fts(id, content) VALUES (new.id, new.content);
            END",

            @"CREATE TRIGGER IF NOT EXISTS memory_observations_ad AFTER DELETE ON memory_observations BEGIN
                DELETE FROM memory_observations_fts WHERE id = old.id;
            END",

            @"CREATE TRIGGER IF NOT EXISTS memory_observations_au AFTER UPDATE ON memory_observations BEGIN
                DELETE FROM memory_observations_fts WHERE id = old.id;
                INSERT INTO memory_observations_fts(id, content) VALUES (new.id, new.content);
            END"
        }}
    };

    private static string fDBPath = string.Empty;
    private static SQLiteConnection fConnection;

    public static string GetDBPath()
    {
        return fDBPath;
    }

    public static void SetDBPath(string path)
    {
        fDBPath = path;
        CheckAndUpdateDatabase(); // Check and update database when setting path
    }

    private static int GetCurrentDBVersion(SQLiteConnection conn)
    {
        try {
            // Check if user_preferences table exists
            var tableExists = conn.ExecuteScalar<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='user_preferences'");

            if (string.IsNullOrEmpty(tableExists))
                return 0; // Database is empty

            // Try to get version from UserPreference table
            var versionRecord = conn.Table<UserPreference>()
                .Where(p => p.PrefKey == "DBVer")
                .FirstOrDefault();

            if (versionRecord != null && int.TryParse(versionRecord.PrefValue, out int version))
                return version;

            // If version record not found but tables exist, assume version 1
            return 1;
        } catch {
            return 0; // Error getting version
        }
    }

    // Set database version in UserPreference table
    private static void SetDBVersion(SQLiteConnection conn, int version)
    {
        var versionRecord = conn.Table<UserPreference>()
            .Where(p => p.PrefKey == "DBVer")
            .FirstOrDefault();

        if (versionRecord == null) {
            versionRecord = new UserPreference() {
                PrefKey = "DBVer",
                PrefValue = version.ToString(),
                ConfidenceScore = 1.0,
                UpdatedAt = DateTime.UtcNow
            };
            conn.Insert(versionRecord);
        } else {
            versionRecord.PrefValue = version.ToString();
            versionRecord.UpdatedAt = DateTime.UtcNow;
            conn.Update(versionRecord);
        }
    }

    // Execute migrations for specified version
    private static void ExecuteMigration(SQLiteConnection conn, int version)
    {
        if (MigrationSQL.TryGetValue(version, out string[] value)) {
            foreach (string sql in value) {
                conn.Execute(sql);
            }
        }
    }

    /// <summary>
    /// Only for tests.
    /// </summary>
    internal static void ClearDatabase()
    {
        CheckConnection();

        fConnection.Execute("DELETE FROM assistant_summary;");
        fConnection.Execute("DELETE FROM assistant_tasks;");
        fConnection.Execute("DELETE FROM memory_relations;");
        fConnection.Execute("DELETE FROM memory_observations;");
        fConnection.Execute("DELETE FROM memory_entities;");
        fConnection.Execute("DELETE FROM user_preferences;");
    }

    private static void CheckConnection()
    {
        if (fConnection != null) return;

        if (File.Exists(fDBPath)) {
            fConnection = new SQLiteConnection(fDBPath);
        } else {
            fConnection = new SQLiteConnection(fDBPath);
            fConnection.ExecuteScalar<string>("PRAGMA journal_mode = WAL;");
            fConnection.ExecuteScalar<string>("PRAGMA auto_vacuum = FULL;");

            // Execute migrations to create tables
            for (int version = 1; version <= CURRENT_DB_VERSION; version++) {
                ExecuteMigration(fConnection, version);
            }
            // Set current database version
            SetDBVersion(fConnection, CURRENT_DB_VERSION);
        }
    }

    // Check database version and execute migrations if necessary
    public static void CheckAndUpdateDatabase()
    {
        if (!File.Exists(fDBPath))
            return; // Database will be created on first connection

        using (var conn = new SQLiteConnection(fDBPath)) {
            int currentVersion = GetCurrentDBVersion(conn);

            // If current version is less than required, execute migrations
            if (currentVersion < CURRENT_DB_VERSION) {
                for (int version = currentVersion + 1; version <= CURRENT_DB_VERSION; version++) {
                    ExecuteMigration(conn, version);
                }

                // Update version in database
                SetDBVersion(conn, CURRENT_DB_VERSION);
            }
        }
    }

    #region Patterns

    /*public static (int totalPatterns, IList<string> uniqueCenturies) GetPatternStats()
    {
        CheckConnection();
        var totalCount = fConnection.ExecuteScalar<int>("select count(*) from [extraction_patterns]");
        var uniqueCenturies = fConnection.QueryScalars<string>("select distinct [century] from [extraction_patterns] where [century] is not null and [century] != ''");
        return (totalCount, uniqueCenturies);
    }

    public static string ExportPatternsToJson()
    {
        var patterns = GetPatterns(null);
        return JsonSerializer.Serialize(patterns, new JsonSerializerOptions { WriteIndented = true });
    }*/

    #endregion

    #region Context

    /// <summary>
    /// Extract context summarization.
    /// </summary>
    internal static AssistantSummary GetSummary(string sessionId)
    {
        CheckConnection();
        return fConnection.Table<AssistantSummary>().Where(s => s.SessionId == sessionId).FirstOrDefault();
    }

    internal static void InsertSummary(AssistantSummary summary)
    {
        CheckConnection();
        fConnection.Insert(summary);
    }

    internal static void UpdateSummary(AssistantSummary summary)
    {
        CheckConnection();
        fConnection.Update(summary);
    }

    #endregion

    #region Profile

    /// <summary>
    /// Extract user profile and preferences
    /// </summary>
    internal static List<UserPreference> GetUserPreferences()
    {
        CheckConnection();
        return fConnection.Table<UserPreference>().ToList();
    }

    internal static UserPreference GetPreference(string normalizedKey)
    {
        CheckConnection();
        return fConnection.Table<UserPreference>().Where(p => p.PrefKey == normalizedKey).FirstOrDefault();
    }

    internal static void InsertPreference(UserPreference newPref)
    {
        CheckConnection();
        fConnection.Insert(newPref);
    }

    internal static void UpdatePreference(UserPreference existingPref)
    {
        CheckConnection();
        fConnection.Update(existingPref);
    }

    internal static void DeletePreference(UserPreference existing)
    {
        CheckConnection();
        fConnection.Delete(existing);
    }

    #endregion

    #region TaskBoard

    /// <summary>
    /// Extract active research tasks (Blackboard)
    /// </summary>
    internal static List<AssistantTask> GetActiveTasks()
    {
        CheckConnection();
        return fConnection.Table<AssistantTask>().Where(t => t.Status == "ACTIVE").ToList();
    }

    internal static AssistantTask GetTask(int taskId)
    {
        CheckConnection();
        return fConnection.Table<AssistantTask>().Where(t => t.TaskId == taskId).FirstOrDefault();
    }

    internal static void InsertTask(AssistantTask value)
    {
        CheckConnection();
        fConnection.Insert(value);
    }

    internal static void UpdateTask(AssistantTask value)
    {
        CheckConnection();
        fConnection.Update(value);
    }

    #endregion

    #region Memory Entries

    internal static List<QMemoryEntity> SearchMemoryEntitiesFTS(string query, int limit = 10)
    {
        CheckConnection();
        try {
            var sql = @"
                WITH matched_entities AS (
                    SELECT DISTINCT mo.entity_id
                    FROM memory_observations_fts fts
                    JOIN memory_observations mo ON mo.id = fts.id
                    WHERE memory_observations_fts MATCH ?
                    ORDER BY bm25(memory_observations_fts)
                    LIMIT ?
                )
                SELECT me.[id], me.[name], me.[type], group_concat(all_mo.content, char(10)) AS [content]
                FROM matched_entities target
                JOIN memory_entities me ON target.entity_id = me.id
                JOIN memory_observations all_mo ON me.id = all_mo.entity_id
                GROUP BY me.id, me.name, me.type";

            return fConnection.Query<QMemoryEntity>(sql, query, limit);
        } catch (Exception ex) {
            // FTS table might not exist yet, fallback to regular search
            return new List<QMemoryEntity>();
        }
    }

    internal static int GetEntityId(string name)
    {
        CheckConnection();

        var sql = @"SELECT me.[id] FROM memory_entities me WHERE me.[name] = ?";
        var entity = fConnection.Query<MemoryEntity>(sql, name).FirstOrDefault();
        return (entity != null) ? entity.Id : 0;
    }

    internal static QMemoryEntity GetEntity(int id)
    {
        CheckConnection();

        var sql = @"
                SELECT me.[id], me.[name], me.[type], group_concat(all_mo.content, char(10)) AS [content]
                FROM memory_entities me
                JOIN memory_observations all_mo ON me.id = all_mo.entity_id
                WHERE me.id = ?
                GROUP BY me.id, me.name, me.type";

        return fConnection.Query<QMemoryEntity>(sql, id).FirstOrDefault();
    }

    internal static List<MemoryObservation> GetAllObservations()
    {
        CheckConnection();
        return fConnection.Table<MemoryObservation>().ToList();
    }

    internal static IList<MemoryObservation> GetObservationEmbeddings()
    {
        CheckConnection();
        return fConnection.Query<MemoryObservation>("select [id], [entity_id], [content], [embedding] from [memory_observations] where [embedding] is not null");
    }

    internal static MemoryObservation GetObservation(int id)
    {
        CheckConnection();
        return fConnection.Table<MemoryObservation>().Where(o => o.Id == id).FirstOrDefault();
    }

    internal static List<MemoryObservation> GetObservationsByEntityId(int entityId)
    {
        CheckConnection();
        return fConnection.Table<MemoryObservation>().Where(o => o.EntityId == entityId).ToList();
    }

    internal static void InsertObservation(MemoryObservation observation)
    {
        CheckConnection();
        fConnection.Insert(observation);
    }

    internal static void UpdateObservation(MemoryObservation observation)
    {
        CheckConnection();
        fConnection.Update(observation);
    }

    internal static void DeleteObservation(MemoryObservation observation)
    {
        CheckConnection();
        fConnection.Delete(observation);
    }

    internal static void DeleteAllObservations(int entityId)
    {
        CheckConnection();
        fConnection.Execute("delete from [memory_observations] where [entity_id] = ?", entityId);
    }

    internal static List<MemoryEntity> GetAllEntities(string type)
    {
        CheckConnection();

        if (string.IsNullOrEmpty(type)) {
            return fConnection.Table<MemoryEntity>().ToList();
        } else {
            return fConnection.Query<MemoryEntity>("select [id], [name], [type] from [memory_entities] where [type] = ?", type);
        }
    }

    internal static void InsertEntity(MemoryEntity entity)
    {
        CheckConnection();
        fConnection.Insert(entity);
    }

    internal static void UpdateEntity(MemoryEntity entity)
    {
        CheckConnection();
        fConnection.Update(entity);
    }

    internal static void DeleteEntity(MemoryEntity entity)
    {
        CheckConnection();
        fConnection.Delete(entity);
    }

    internal static MemoryRelation GetRelation(int src, int trg, string pred)
    {
        CheckConnection();
        return fConnection.Table<MemoryRelation>().Where(r => r.SourceEntityId == src && r.Predicate == pred && r.TargetEntityId == trg).FirstOrDefault();
    }

    internal static MemoryRelation GetRelationById(int relationId)
    {
        CheckConnection();
        return fConnection.Table<MemoryRelation>().Where(r => r.RelationId == relationId).FirstOrDefault();
    }

    internal static List<MemoryRelation> GetRelationBySource(int id)
    {
        CheckConnection();
        return fConnection.Table<MemoryRelation>().Where(r => r.SourceEntityId == id).ToList();
    }

    internal static List<MemoryRelation> GetRelationByTarget(int id)
    {
        CheckConnection();
        return fConnection.Table<MemoryRelation>().Where(r => r.TargetEntityId == id).ToList();
    }

    internal static void InsertRelation(MemoryRelation relation)
    {
        CheckConnection();
        fConnection.Insert(relation);
    }

    internal static void UpdateRelation(MemoryRelation relation)
    {
        CheckConnection();
        fConnection.Update(relation);
    }

    internal static void DeleteRelation(MemoryRelation relation)
    {
        CheckConnection();
        fConnection.Delete(relation);
    }

    #endregion
}
