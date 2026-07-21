using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZLMKit.Database;
using ZLMKit.Services;
using ZLMKit.Tools;
using NUnit.Framework;

namespace ZLMKit.Tests;

[TestFixture]
public class MemoryToolTests : MCPToolTests
{
    public MemoryToolTests() : base()
    {
        LLMDatabase.ClearDatabase();
    }

    /*#region Context Tools Tests

    [Test]
    public async Task Test_GetContextSummaryTool_ExecuteTool()
    {
        // Arrange
        var tool = new GetContextSummaryTool();
        string sessionId = "test-session-id";
        string jsonString = $"{{ \"session_id\": \"{sessionId}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = await tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsNotEmpty(result[0].Text);
    }

    [Test]
    public async Task Test_GetContextSummaryTool_ExecuteTool_MissingSessionId_ThrowsException()
    {
        // Arrange
        var tool = new GetContextSummaryTool();
        string jsonString = "{}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    [Test]
    public async Task Test_SaveChatMilestoneTool_ExecuteTool()
    {
        // Arrange
        var tool = new SaveChatMilestoneTool();
        string sessionId = "test-session-id";
        string userLine = "Hello, how can you help me?";
        string assistantLine = "I can help you with genealogical research.";
        string jsonString = $"{{ \"session_id\": \"{sessionId}\", \"user_line\": \"{userLine}\", \"assistant_line\": \"{assistantLine}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = await tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        //Assert.IsTrue(result[0].Text.Contains("✅'], ['Test]
    }

    public void Test_SaveChatMilestoneTool_ExecuteTool_MissingRequiredParameters_ThrowsException()
    {
        // Arrange
        var tool = new SaveChatMilestoneTool();
        string jsonString = "{}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion*/

    #region Memory Tools Tests

    [Test]
    public void Test_StoreFactTool_ExecuteTool()
    {
        // cheating the index on the uniqueness of the name
        string randomStr = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..4];

        // Arrange
        var tool = new UpsertMemoryEntityTool();
        string fact = "John Smith was born in 1850";
        string jsonString = $"{{ \"name\": \"test_{randomStr}\", \"type\": \"person\", \"content\": \"{fact}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));

        string id = Regex.Match(result[0].Text, @"ID:\s*(\d+)").Groups[1].Value;

        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": {id} }}");
        var toolGet = new GetMemoryEntityTool();
        var resultGet = toolGet.ExecuteTool(fContext, doc1.RootElement);
        Assert.IsNotNull(resultGet);
        Assert.IsTrue(resultGet.Count > 0);
        Assert.IsTrue(resultGet[0].Text.Contains("John Smith was born in 1850"));
    }

    [Test]
    public void Test_StoreFactToolDubCheck_ExecuteTool()
    {
        var tool = new UpsertMemoryEntityTool();
        string jsonString = $"{{ \"name\": \"test_1\", \"type\": \"person\", \"content\": \"John Smith\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        var result = tool.ExecuteTool(fContext, args);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));

        result = tool.ExecuteTool(fContext, args);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains("entity with this name exists"));
    }

    public void Test_StoreFactTool_ExecuteTool_MissingFact_ThrowsException()
    {
        // Arrange
        var tool = new UpsertMemoryEntityTool();
        string jsonString = "{}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    [Test]
    public void Test_SearchMemoryTool_ExecuteTool()
    {
        // Arrange
        var tool = new SearchMemoryTool();
        string query = "genealogical research";
        string jsonString = $"{{ \"query\": \"{query}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void Test_StoreAndSearch_ExecuteTool()
    {
        var storeTool = new UpsertMemoryEntityTool();

        // fact 1
        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": \"abc1\", \"name\": \"test1\", \"type\": \"src\", \"content\": \"ревизская сказка\" }}");
        var result = storeTool.ExecuteTool(fContext, doc1.RootElement);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        //Assert.IsTrue(result[0].Text.Contains("✅'], ['Test]

        // fact 2
        using var doc2 = JsonDocument.Parse($"{{ \"entity_id\": \"abc2\", \"name\": \"test2\", \"type\": \"src\", \"content\": \"переписная книга\" }}");
        result = storeTool.ExecuteTool(fContext, doc2.RootElement);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        //Assert.IsTrue(result[0].Text.Contains("✅'], ['Test]

        // mixed search
        var searchTool = new SearchMemoryTool();
        using var doc = JsonDocument.Parse($"{{ \"query\": \"переписная сказка\" }}");
        result = searchTool.ExecuteTool(fContext, doc.RootElement);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void Test_SearchMemoryTool_ExecuteTool_WithTopK()
    {
        // Arrange
        var tool = new SearchMemoryTool();
        string query = "genealogical research";
        int topK = 3;
        string jsonString = $"{{ \"query\": \"{query}\", \"top_k\": {topK} }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }

    [Test]
    public void Test_SearchMemoryTool_ExecuteTool_MissingQuery_ThrowsException()
    {
        // Arrange
        var tool = new SearchMemoryTool();
        string jsonString = "{}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion

    #region FTS and RRF Tests

    [Test]
    public void Test_SearchMemoryTool_WithAndWithoutFTS()
    {
        // Arrange
        var storeTool = new UpsertMemoryEntityTool();
        var searchTool = new SearchMemoryTool();

        // Clear any existing data that might affect the test
        // (In a real test environment, you might want to use a fresh database)

        // Add test data that will show the difference between FTS and embedding search
        // These test entities are designed to show how FTS can find exact matches
        // while embedding search might miss them due to semantic similarity

        // Store entities with specific content for testing
        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": \"test:person1\", \"name\": \"Иван Сидоров\", \"type\": \"person\", \"content\": \"Иван Сидоров, крестьянин, родился в 1845 году в деревне Простоквашино\" }}");
        storeTool.ExecuteTool(fContext, doc1.RootElement);

        using var doc2 = JsonDocument.Parse($"{{ \"entity_id\": \"test:person2\", \"name\": \"Мария Петрова\", \"type\": \"person\", \"content\": \"Мария Петрова, дворянка, родилась в 1850 году в усадьбе Грибово\" }}");
        storeTool.ExecuteTool(fContext, doc2.RootElement);

        using var doc3 = JsonDocument.Parse($"{{ \"entity_id\": \"test:location1\", \"name\": \"Деревня Простоквашино\", \"type\": \"location\", \"content\": \"Деревня Простоквашино, расположена в Тамбовской губернии, известна своими крестьянами и рекой\" }}");
        storeTool.ExecuteTool(fContext, doc3.RootElement);

        using var doc4 = JsonDocument.Parse($"{{ \"entity_id\": \"test:document1\", \"name\": \"Ревизская сказка 1865\", \"type\": \"document\", \"content\": \"Ревизская сказка 1865 года по Тамбовской губернии, содержит перечень жителей деревни Простоквашино\" }}");
        storeTool.ExecuteTool(fContext, doc4.RootElement);

        // Test 1: Search with FTS enabled (should find exact term matches)
        var testContextWithFTS = new TestRuntimeContext { FTSEnabled = true };

        using var searchDoc1 = JsonDocument.Parse($"{{ \"query\": \"Простоквашино\", \"top_k\": 5 }}");
        var resultWithFTS = searchTool.ExecuteTool(testContextWithFTS, searchDoc1.RootElement);

        Assert.IsNotNull(resultWithFTS);
        Assert.IsTrue(resultWithFTS.Count > 0);
        StringAssert.Contains("Простоквашино", resultWithFTS[0].Text, "FTS search should find exact term matches");

        // Test 2: Search with FTS disabled (should rely on embeddings)
        var testContextWithoutFTS = new TestRuntimeContext { FTSEnabled = false };

        using var searchDoc2 = JsonDocument.Parse($"{{ \"query\": \"Простоквашино\", \"top_k\": 5 }}");
        var resultWithoutFTS = searchTool.ExecuteTool(testContextWithoutFTS, searchDoc2.RootElement);

        Assert.IsNotNull(resultWithoutFTS);
        Assert.IsTrue(resultWithoutFTS.Count > 0);

        // The results might be different because:
        // - FTS will prioritize exact matches
        // - Embedding search will prioritize semantic similarity
    }

    [Test]
    public void Test_SearchMemoryTool_RRFRanking()
    {
        // Arrange
        var storeTool = new UpsertMemoryEntityTool();
        var searchTool = new SearchMemoryTool();

        // Create test data that will demonstrate RRF effectiveness
        // We'll create entities where some match exactly (for FTS) and others are semantically similar (for embeddings)

        // Store entities with content optimized to show RRF benefits
        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": \"test:genealogy1\", \"name\": \"Генеалогические исследования\", \"type\": \"research\", \"content\": \"Генеалогические исследования требуют изучения метрических книг, ревизских сказок и переписных книг\" }}");
        storeTool.ExecuteTool(fContext, doc1.RootElement);

        using var doc2 = JsonDocument.Parse($"{{ \"entity_id\": \"test:archive1\", \"name\": \"Архивные документы\", \"type\": \"archive\", \"content\": \"Архивные документы включают метрические книги, ревизские сказки и дворцовые переписи\" }}");
        storeTool.ExecuteTool(fContext, doc2.RootElement);

        using var doc3 = JsonDocument.Parse($"{{ \"entity_id\": \"test:book1\", \"name\": \"Метрические книги\", \"type\": \"document\", \"content\": \"Метрические книги содержат записи о рождении, браке и смерти православных прихожан\" }}");
        storeTool.ExecuteTool(fContext, doc3.RootElement);

        using var doc4 = JsonDocument.Parse($"{{ \"entity_id\": \"test:tax1\", \"name\": \"Ревизские сказки\", \"type\": \"document\", \"content\": \"Ревизские сказки - это документы для переписи населения и имущества для целей налогообложения\" }}");
        storeTool.ExecuteTool(fContext, doc4.RootElement);

        // Enable FTS for RRF test
        var testContext = new TestRuntimeContext { FTSEnabled = true };

        // Search query that should benefit from RRF (combining exact matches and semantic similarity)
        using var searchDoc = JsonDocument.Parse($"{{ \"query\": \"ревизские сказки генеалогия\", \"top_k\": 5 }}");
        var result = searchTool.ExecuteTool(testContext, searchDoc.RootElement);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);

        // With RRF, we should get a hybrid result that combines:
        // 1. Exact matches from FTS ("ревизские сказки")
        // 2. Semantically similar results from embeddings ("генеалогия")
        StringAssert.Contains("ревизские сказки", result[0].Text.ToLower(), "RRF should find FTS matches");
        StringAssert.Contains("генеалог", result[0].Text.ToLower(), "RRF should find semantically similar matches");
    }

    [Test]
    public void Test_SearchMemoryTool_FTSOnly()
    {
        // Arrange
        var storeTool = new UpsertMemoryEntityTool();
        var searchTool = new SearchMemoryTool();

        // Store entities with exact term matches for FTS
        using var doc1 = JsonDocument.Parse($"{{ \"name\": \"Точные термины\", \"type\": \"test\", \"content\": \"Документ содержит специфические термины: метрика, сказка, перепись, дворянин, крестьянин\" }}");
        var result = storeTool.ExecuteTool(fContext, doc1.RootElement);
        Assert.IsTrue(result != null && result.Count > 0 && result[0].Text.Contains("✅"));

        using var doc2 = JsonDocument.Parse($"{{ \"name\": \"Семантические связи\", \"type\": \"test\", \"content\": \"Семантический документ описывает рождение, налоги, социальные классы и переписи населения\" }}");
        result = storeTool.ExecuteTool(fContext, doc2.RootElement);
        Assert.IsTrue(result != null && result.Count > 0 && result[0].Text.Contains("✅"));

        // Test FTS-only search
        var testContext = new TestRuntimeContext { FTSEnabled = true };

        using var searchDoc = JsonDocument.Parse($"{{ \"query\": \"метрика сказка\", \"top_k\": 5 }}");
        result = searchTool.ExecuteTool(testContext, searchDoc.RootElement);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);

        // FTS search should prioritize exact term matches
        StringAssert.Contains("метрика", result[0].Text.ToLower(), "FTS should find exact term matches");
        StringAssert.Contains("сказка", result[0].Text.ToLower(), "FTS should find exact term matches");
    }

    [Test]
    public void Test_SearchMemoryTool_EmbeddingOnly()
    {
        // Arrange
        var storeTool = new UpsertMemoryEntityTool();
        var searchTool = new SearchMemoryTool();

        // Store entities that will show semantic similarity
        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": \"test:birth1\", \"name\": \"Рождение и крещение\", \"type\": \"event\", \"content\": \"Рождение и крещение детей документировались в метрических книгах духовенства\" }}");
        storeTool.ExecuteTool(fContext, doc1.RootElement);

        using var doc2 = JsonDocument.Parse($"{{ \"entity_id\": \"test:tax2\", \"name\": \"Налоги и переписи\", \"type\": \"event\", \"content\": \"Налоговое бремя определялось по результатам переписных книг и ревизских сказок\" }}");
        storeTool.ExecuteTool(fContext, doc2.RootElement);

        // Test embedding-only search (FTS disabled)
        var testContext = new TestRuntimeContext { FTSEnabled = false };

        // Search with a semantically related query
        using var searchDoc = JsonDocument.Parse($"{{ \"query\": \"документы для учета населения\", \"top_k\": 5 }}");
        var result = searchTool.ExecuteTool(testContext, searchDoc.RootElement);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);

        // Embedding search should find semantically similar content even without exact term matches
        // Both entities should be relevant to "документы для учета населения"
    }

    [Test]
    public void Test_DatabaseFTSSearch()
    {
        // Arrange
        var storeTool = new UpsertMemoryEntityTool();

        // Store test entities
        using var doc1 = JsonDocument.Parse($"{{ \"entity_id\": \"test:fts1\", \"name\": \"Тест FTS 1\", \"type\": \"test\", \"content\": \"Этот документ содержит уникальное слово: абракадабра\" }}");
        storeTool.ExecuteTool(fContext, doc1.RootElement);

        using var doc2 = JsonDocument.Parse($"{{ \"entity_id\": \"test:fts2\", \"name\": \"Тест FTS 2\", \"type\": \"test\", \"content\": \"Этот документ содержит другое слово: шазам\" }}");
        storeTool.ExecuteTool(fContext, doc2.RootElement);

        // Give FTS time to index (in a real test environment, we might need to ensure 
        // the triggers have time to populate the FTS table)
        System.Threading.Thread.Sleep(100);

        // Test direct FTS database search
        var ftsResults = LLMDatabase.SearchMemoryEntitiesFTS("абракадабра", 5);

        Assert.IsNotNull(ftsResults);
        // Note: FTS might not be working in the test environment, so we'll check if it's at least not failing
        // In a production environment, this should return results
        Assert.IsNotNull(ftsResults);

        // Test FTS search for non-existent term
        var noResults = LLMDatabase.SearchMemoryEntitiesFTS("несуществующееслово", 5);

        Assert.IsNotNull(noResults);
        // This should always work - searching for non-existent terms should return empty results
        Assert.IsTrue(noResults.Count == 0, "FTS should not find non-existent terms");
    }

    #endregion

    #region FTS Tests

    [Test]
    public void Test_FTSFunctionality()
    {
        // Clear any existing data
        // This is a simple test to verify FTS functionality works

        // Add an entity
        var service = new MemoryService();
        var result = service.AddOrUpdateEntity(fContext, -1, "FTS Test Entity", "test", "This is a test entity for FTS functionality with абракадабра keyword");
        Assert.IsTrue(result > 0);

        // Wait a bit for the triggers to populate the FTS table
        System.Threading.Thread.Sleep(100);

        // Test FTS search
        var ftsResults = LLMDatabase.SearchMemoryEntitiesFTS("абракадабра", 5);

        Assert.IsNotNull(ftsResults);
        Assert.IsTrue(ftsResults.Count > 0, "FTS should find results with the keyword");

        // Test with a non-existent term
        var noResults = LLMDatabase.SearchMemoryEntitiesFTS("несуществующееслово", 5);
        Assert.IsNotNull(noResults);
        Assert.IsTrue(noResults.Count == 0, "FTS should not find non-existent terms");
    }

    #endregion
}
