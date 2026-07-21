using System;
using System.Text.Json;
using System.Collections.Generic;
using ZLMKit.Database;
using ZLMKit.Tools;
using NUnit.Framework;

namespace ZLMKit.Tests;

[TestFixture]
public class ProfileToolTests : MCPToolTests
{
    public ProfileToolTests() : base()
    {
        LLMDatabase.ClearDatabase();
    }

    #region GetUserProfileTool Tests

    [Test]
    public void Test_GetUserProfileTool_ExecuteTool_EmptyProfile()
    {
        // Arrange
        var tool = new GetUserProfileTool();
        string jsonString = "{}";

        // Act
        var result = ExecTool(tool, fContext, jsonString);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains("User profile is empty"));
    }

    [Test]
    public void Test_GetUserProfileTool_ExecuteTool_WithPreferences()
    {
        // Arrange
        // First add some preferences
        var updateTool = new UpdateUserProfileTool();
        string jsonString1 = $"{{ \"key\": \"research_focus\", \"value\": \"genealogy\" }}";
        ExecTool(updateTool, fContext, jsonString1);

        string jsonString2 = $"{{ \"key\": \"preferred_language\", \"value\": \"English\" }}";
        ExecTool(updateTool, fContext, jsonString2);

        var tool = new GetUserProfileTool();
        string jsonString = "{}";

        // Act
        var result = ExecTool(tool, fContext, jsonString);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("{"));
        Assert.IsTrue(result[0].Text.Contains("}"));

        // Parse the JSON result to verify content
        try
        {
            var preferences = JsonSerializer.Deserialize<Dictionary<string, string>>(result[0].Text);
            Assert.IsNotNull(preferences);
            Assert.IsTrue(preferences.ContainsKey("research_focus"));
            Assert.IsTrue(preferences.ContainsKey("preferred_language"));
            Assert.AreEqual("genealogy", preferences["research_focus"]);
            Assert.AreEqual("English", preferences["preferred_language"]);
        }
        catch (JsonException)
        {
            Assert.Fail("Result should be valid JSON");
        }
    }

    #endregion

    #region UpdateUserProfileTool Tests

    [Test]
    public void Test_UpdateUserProfileTool_ExecuteTool()
    {
        // Arrange
        var tool = new UpdateUserProfileTool();
        string key = "research_focus";
        string value = "genealogy";
        string jsonString = $"{{ \"key\": \"{key}\", \"value\": \"{value}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains("successfully updated"));

        // Verify preference was saved in database
        var preferences = LLMDatabase.GetUserPreferences();
        Assert.IsTrue(preferences.Count > 0);
        var pref = preferences.Find(p => p.PrefKey == key);
        Assert.IsNotNull(pref);
        Assert.AreEqual(value, pref.PrefValue);
    }

    [Test]
    public void Test_UpdateUserProfileTool_ExecuteTool_UpdateExisting()
    {
        // Arrange
        var tool = new UpdateUserProfileTool();
        string key = "research_focus";
        string value1 = "genealogy";
        string value2 = "military records";

        // First set a preference
        string jsonString1 = $"{{ \"key\": \"{key}\", \"value\": \"{value1}\" }}";
        using var doc1 = JsonDocument.Parse(jsonString1);
        JsonElement args1 = doc1.RootElement;
        tool.ExecuteTool(fContext, args1);

        // Then update it
        string jsonString2 = $"{{ \"key\": \"{key}\", \"value\": \"{value2}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString2);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));

        // Verify preference was updated in database
        var preferences = LLMDatabase.GetUserPreferences();
        Assert.IsTrue(preferences.Count > 0);
        var pref = preferences.Find(p => p.PrefKey == key);
        Assert.IsNotNull(pref);
        Assert.AreEqual(value2, pref.PrefValue);
    }

    [Test]
    public void Test_UpdateUserProfileTool_ExecuteTool_EmptyKey()
    {
        // Arrange
        var tool = new UpdateUserProfileTool();
        string key = "";
        string value = "genealogy";
        string jsonString = $"{{ \"key\": \"{key}\", \"value\": \"{value}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains("key is empty or invalid"));
    }

    [Test]
    public void Test_UpdateUserProfileTool_ExecuteTool_MissingKey_ThrowsException()
    {
        // Arrange
        var tool = new UpdateUserProfileTool();
        string jsonString = $"{{ \"value\": \"genealogy\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    [Test]
    public void Test_UpdateUserProfileTool_ExecuteTool_MissingValue_ThrowsException()
    {
        // Arrange
        var tool = new UpdateUserProfileTool();
        string jsonString = $"{{ \"key\": \"research_focus\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion

    #region RemoveUserPreferenceTool Tests

    [Test]
    public void Test_RemoveUserPreferenceTool_ExecuteTool()
    {
        // Arrange
        // First add a preference
        var updateTool = new UpdateUserProfileTool();
        string key = "research_focus";
        string value = "genealogy";
        string jsonString1 = $"{{ \"key\": \"{key}\", \"value\": \"{value}\" }}";
        using var doc1 = JsonDocument.Parse(jsonString1);
        JsonElement args1 = doc1.RootElement;
        updateTool.ExecuteTool(fContext, args1);

        var tool = new RemoveUserPreferenceTool();
        string jsonString2 = $"{{ \"key\": \"{key}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString2);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains("successfully removed"));

        // Verify preference was removed from database
        var preferences = LLMDatabase.GetUserPreferences();
        var pref = preferences.Find(p => p.PrefKey == key);
        Assert.IsNull(pref);
    }

    [Test]
    public void Test_RemoveUserPreferenceTool_ExecuteTool_NonExistentKey()
    {
        // Arrange
        var tool = new RemoveUserPreferenceTool();
        string key = "non_existent_key";
        string jsonString = $"{{ \"key\": \"{key}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains($"Parameter '{key}' not found"));
    }

    [Test]
    public void Test_RemoveUserPreferenceTool_ExecuteTool_MissingKey_ThrowsException()
    {
        // Arrange
        var tool = new RemoveUserPreferenceTool();
        string jsonString = "{}";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ExecTool(tool, fContext, jsonString));
    }

    #endregion
}