/*
 *  ZAssistant, the personal LLM Assistant.
 *  Copyright (C) 2026 by Sergey V. Zhdanovskih.
 *
 *  Licensed under the GNU General Public License (GPL) v3.
 *  See LICENSE file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using ZLMKit.Protocols;

namespace ZLMKit.Providers;

public interface ILMChatProvider
{
    void CancelRequest();
    Task<List<ModelData>> LoadModelsAsync();
    Task<string> SendMessageSingleAsync(string role, string content, float temperature);
    Task SendMessageAsync();
}
