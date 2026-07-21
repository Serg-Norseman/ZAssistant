/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Text.Json;
using ZLMKit.MCP;
using ZLMKit.Protocols;
using ZLMKit.Services;

namespace ZLMKit.Tools;


internal class CreateAssistantTaskTool : BaseTool
{
    public CreateAssistantTaskTool() : base("create_assistant_task") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Create a new assistant task with a specific objective",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["objective"] = new MCPToolProperty { Type = "string", Description = "The objective or focus of the task" },
                    ["description"] = new MCPToolProperty { Type = "string", Description = "Detailed description of what needs to be accomplished" }
                },
                Required = new List<string> { "objective", "description" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        string objective = MCPHelper.GetRequiredStr(args, "objective");
        string description = MCPHelper.GetRequiredStr(args, "description");

        var service = new MemoryService();
        int newId = service.CreateTask(objective, description);

        return MCPContent.CreateSimpleContent($"✅ Created new task #{newId}. Focus on completing this task.");
    }
}


internal class UpdateAssistantTaskTool : BaseTool
{
    public UpdateAssistantTaskTool() : base("update_assistant_task") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Update progress on an existing task: add completed steps and define next steps",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["task_id"] = new MCPToolProperty { Type = "integer", Description = "The unique ID of the task to update" },
                    ["add_completed_step"] = new MCPToolProperty { Type = "string", Description = "Description of a newly completed step or finding", Default = "" },
                    ["set_next_steps"] = new MCPToolProperty {
                        Type = "array",
                        Description = "List of next action steps for the task",
                        Items = new MCPToolProperty { Type = "string" }
                    }
                },
                Required = new List<string> { "task_id" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int taskId = MCPHelper.GetRequiredInt(args, "task_id");
        string addCompletedStep = MCPHelper.GetOptionalStr(args, "add_completed_step", "");
        string[] setNextSteps = MCPHelper.GetOptionalStringArray(args, "set_next_steps", Array.Empty<string>());

        var service = new MemoryService();
        bool success = service.UpdateTaskProgress(taskId, addCompletedStep, setNextSteps);

        if (!success)
            return MCPContent.CreateSimpleContent($"❌ Task with ID {taskId} not found.");

        return MCPContent.CreateSimpleContent($"✅ Progress on task #{taskId} updated. Knowledge base has recorded the changes.");
    }
}


internal class ChangeTaskStatusTool : BaseTool
{
    public ChangeTaskStatusTool() : base("change_task_status") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Change the status of a research task (COMPLETED, PAUSED, or ACTIVE)",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["task_id"] = new MCPToolProperty { Type = "integer", Description = "The unique ID of the task" },
                    ["status"] = new MCPToolProperty { Type = "string", Description = "New status value: COMPLETED, PAUSED, or ACTIVE" }
                },
                Required = new List<string> { "task_id", "status" }
            },
            Annotations = new MCPToolAnnotations() {
                ReadOnlyHint = false,
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        int taskId = MCPHelper.GetRequiredInt(args, "task_id");
        string status = MCPHelper.GetRequiredStr(args, "status");

        var service = new MemoryService();
        bool success = service.ChangeTaskStatus(taskId, status);

        if (!success)
            return MCPContent.CreateSimpleContent($"❌ Failed to change status for task #{taskId}. Verify ID and status value (COMPLETED/PAUSED/ACTIVE).");

        return MCPContent.CreateSimpleContent($"✅ Task #{taskId} status changed to {status.ToUpperInvariant()}.");
    }
}
