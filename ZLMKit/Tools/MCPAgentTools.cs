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
using BSLib;
using ZLMKit.MCP;
using ZLMKit.Protocols;

namespace ZLMKit.Tools;

internal class GetCurrentTimeTool : BaseTool
{
    public GetCurrentTimeTool() : base("get_current_time") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Retrieve current local date/time in the system time zone.",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> { },
                Required = []
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        try {
            var result = DateTime.Now.ToString(context.DefaultTimeFormat);

            return MCPContent.CreateSimpleContent($"✅ Current date/time: {result}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error getting date/time: {ex.Message}");
        }
    }
}


internal class ExpressionCalculatorTool : BaseTool
{
    private static readonly ExpCalculator fCalculator = new ExpCalculator();

    public ExpressionCalculatorTool() : base("expression_calculator") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Calculator for evaluating mathematical expressions. Supports arithmetic operations (+,-,*,/,** pow,! not,% mod,& and,| or,^ xor,~ inv), mathematical functions (round,trunc,int,frac,sin,cos,tan,atan,ln,exp,sign), boolean logic (if(condition;then_expr;else_expr)), variables (a=5;b=4;a+b), consts (pi,e), and conditional expressions (<, <=, >, >=, ==, !=).",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> {
                    ["expression"] = new MCPToolProperty {
                        Type = "string",
                        Description = "Mathematical expression to evaluate."
                    }
                },
                Required = ["expression"]
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        try {
            string expression = MCPHelper.GetRequiredStr(args, "expression");
            double result = fCalculator.Calc(expression);

            return MCPContent.CreateSimpleContent($"✅ Result: {result}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error calculating expression: {ex.Message}");
        }
    }
}


internal class NewUIDTool : BaseTool
{
    public NewUIDTool() : base("new_uid") { }

    public override MCPTool CreateTool()
    {
        return new MCPTool {
            Name = Sign,
            Description = "Generates a new unique identifier (UID; string format, 32 chars).",
            InputSchema = new MCPToolInputSchema {
                Properties = new Dictionary<string, MCPToolProperty> { },
                Required = []
            }
        };
    }

    public override List<MCPContent> ExecuteTool(IRuntimeContext context, JsonElement args)
    {
        try {
            string uid = Guid.NewGuid().ToString("N");
            return MCPContent.CreateSimpleContent($"✅ New UID: {uid}");
        } catch (Exception ex) {
            return MCPContent.CreateSimpleContent($"❌ Error generating UID: {ex.Message}");
        }
    }
}
