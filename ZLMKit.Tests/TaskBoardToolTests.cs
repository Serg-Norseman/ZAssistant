using System;
using System.Text.Json;
using System.Linq;
using ZLMKit.Database;
using ZLMKit.Tools;
using NUnit.Framework;

namespace ZLMKit.Tests;

[TestFixture]
public class TaskBoardToolTests : MCPToolTests
{
    public TaskBoardToolTests() : base()
    {
        LLMDatabase.ClearDatabase();
    }

    #region CreateAssistantTaskTool Tests

    [Test]
    public void Test_CreateAssistantTaskTool_ExecuteTool()
    {
        // Arrange
        var tool = new CreateAssistantTaskTool();
        string objective = "Research family history";
        string description = "Find information about the Ivanov family lineage";
        string jsonString = $"{{ \"objective\": \"{objective}\", \"description\": \"{description}\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains("Created new task"));

        // Verify task was created in database
        var tasks = LLMDatabase.GetActiveTasks();
        Assert.IsTrue(tasks.Count > 0);
        Assert.AreEqual(objective, tasks[0].Objective);
        Assert.AreEqual(description, tasks[0].Description);
    }

    [Test]
    public void Test_CreateAssistantTaskTool_ExecuteTool_MissingObjective_ThrowsException()
    {
        // Arrange
        var tool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    [Test]
    public void Test_CreateAssistantTaskTool_ExecuteTool_MissingDescription_ThrowsException()
    {
        // Arrange
        var tool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion

    #region UpdateAssistantTaskTool Tests

    [Test]
    public void Test_UpdateAssistantTaskTool_ExecuteTool_AddCompletedStep()
    {
        // Arrange
        var createTool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\", \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        var createResult = createTool.ExecuteTool(fContext, args);

        // Extract task ID from result
        string resultText = createResult[0].Text;
        int taskId = int.Parse(resultText.Split('#')[1].Split('.')[0]);

        var tool = new UpdateAssistantTaskTool();
        string completedStep = "Found birth record for Ivan Ivanov in 1850";
        jsonString = $"{{ \"task_id\": {taskId}, \"add_completed_step\": \"{completedStep}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains("updated"));

        // Verify task was updated in database
        var task = LLMDatabase.GetTask(taskId);
        Assert.IsNotNull(task);
        var completedSteps = JsonSerializer.Deserialize<string[]>(task.CompletedStepsJson);
        Assert.IsTrue(completedSteps.Contains(completedStep));
    }

    [Test]
    public void Test_UpdateAssistantTaskTool_ExecuteTool_SetNextSteps()
    {
        // Arrange
        var createTool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\", \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        var createResult = createTool.ExecuteTool(fContext, args);

        // Extract task ID from result
        string resultText = createResult[0].Text;
        int taskId = int.Parse(resultText.Split('#')[1].Split('.')[0]);

        var tool = new UpdateAssistantTaskTool();
        string[] nextSteps = new string[] { "Search marriage records", "Check census data" };
        // Properly format the JSON array
        string nextStepsJson = "[\"Search marriage records\", \"Check census data\"]";
        jsonString = $"{{ \"task_id\": {taskId}, \"set_next_steps\": {nextStepsJson} }}";
        using var doc2 = JsonDocument.Parse(jsonString);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));

        // Verify task was updated in database
        var task = LLMDatabase.GetTask(taskId);
        Assert.IsNotNull(task);
        var storedNextSteps = JsonSerializer.Deserialize<string[]>(task.NextStepsJson);
        Assert.AreEqual(nextSteps.Length, storedNextSteps.Length);
        for (int i = 0; i < nextSteps.Length; i++)
        {
            Assert.AreEqual(nextSteps[i], storedNextSteps[i]);
        }
    }

    [Test]
    public void Test_UpdateAssistantTaskTool_ExecuteTool_InvalidTaskId()
    {
        // Arrange
        var tool = new UpdateAssistantTaskTool();
        int invalidTaskId = 999999;
        string jsonString = $"{{ \"task_id\": {invalidTaskId}, \"add_completed_step\": \"Some step\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains($"ID {invalidTaskId} not found"));
    }

    [Test]
    public void Test_UpdateAssistantTaskTool_ExecuteTool_MissingTaskId_ThrowsException()
    {
        // Arrange
        var tool = new UpdateAssistantTaskTool();
        string jsonString = $"{{ \"add_completed_step\": \"Some step\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion

    #region ChangeTaskStatusTool Tests

    [Test]
    public void Test_ChangeTaskStatusTool_ExecuteTool_ChangeToCompleted()
    {
        // Arrange
        var createTool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\", \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        var createResult = createTool.ExecuteTool(fContext, args);

        // Extract task ID from result
        string resultText = createResult[0].Text;
        int taskId = int.Parse(resultText.Split('#')[1].Split('.')[0]);

        var tool = new ChangeTaskStatusTool();
        string newStatus = "COMPLETED";
        jsonString = $"{{ \"task_id\": {taskId}, \"status\": \"{newStatus}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains(newStatus));

        // Verify task status was updated in database
        var task = LLMDatabase.GetTask(taskId);
        Assert.IsNotNull(task);
        Assert.AreEqual(newStatus, task.Status);
    }

    [Test]
    public void Test_ChangeTaskStatusTool_ExecuteTool_ChangeToPaused()
    {
        // Arrange
        var createTool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\", \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        var createResult = createTool.ExecuteTool(fContext, args);

        // Extract task ID from result
        string resultText = createResult[0].Text;
        int taskId = int.Parse(resultText.Split('#')[1].Split('.')[0]);

        var tool = new ChangeTaskStatusTool();
        string newStatus = "PAUSED";
        jsonString = $"{{ \"task_id\": {taskId}, \"status\": \"{newStatus}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("✅"));
        Assert.IsTrue(result[0].Text.Contains(newStatus));

        // Verify task status was updated in database
        var task = LLMDatabase.GetTask(taskId);
        Assert.IsNotNull(task);
        Assert.AreEqual(newStatus, task.Status);
    }

    [Test]
    public void Test_ChangeTaskStatusTool_ExecuteTool_InvalidStatus()
    {
        // Arrange
        var createTool = new CreateAssistantTaskTool();
        string jsonString = $"{{ \"objective\": \"Research family history\", \"description\": \"Find information about the Ivanov family lineage\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;
        var createResult = createTool.ExecuteTool(fContext, args);

        // Extract task ID from result
        string resultText = createResult[0].Text;
        int taskId = int.Parse(resultText.Split('#')[1].Split('.')[0]);

        var tool = new ChangeTaskStatusTool();
        string invalidStatus = "INVALID_STATUS";
        jsonString = $"{{ \"task_id\": {taskId}, \"status\": \"{invalidStatus}\" }}";
        using var doc2 = JsonDocument.Parse(jsonString);
        JsonElement args2 = doc2.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args2);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains("Failed to change status"));

        // Verify task status was NOT updated in database
        var task = LLMDatabase.GetTask(taskId);
        Assert.IsNotNull(task);
        Assert.AreEqual("ACTIVE", task.Status); // Should still be ACTIVE
    }

    [Test]
    public void Test_ChangeTaskStatusTool_ExecuteTool_InvalidTaskId()
    {
        // Arrange
        var tool = new ChangeTaskStatusTool();
        int invalidTaskId = 999999;
        string jsonString = $"{{ \"task_id\": {invalidTaskId}, \"status\": \"COMPLETED\" }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act
        var result = tool.ExecuteTool(fContext, args);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
        Assert.IsTrue(result[0].Text.Contains("❌"));
        Assert.IsTrue(result[0].Text.Contains($"Failed to change status for task #{invalidTaskId}"));
    }

    [Test]
    public void Test_ChangeTaskStatusTool_ExecuteTool_MissingStatus_ThrowsException()
    {
        // Arrange
        var tool = new ChangeTaskStatusTool();
        string jsonString = $"{{ \"task_id\": 1 }}";
        using var doc = JsonDocument.Parse(jsonString);
        JsonElement args = doc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tool.ExecuteTool(fContext, args));
    }

    #endregion
}