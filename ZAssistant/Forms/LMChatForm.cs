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
using System.Text.Json;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using ZAssistant.Utilities;
using ZLMKit.LMChat;
using ZLMKit.MCP;
using ZLMKit.Utilities;

namespace ZAssistant.Forms
{
    public class LMChatForm : Form, ILMChatView
    {
        private readonly ILangMan fLangMan;
        private readonly LMChatClient fLMClient;
        private readonly LMSettings fLMSettings;


        public LMChatForm(ZARuntimeContext context)
        {
            fLangMan = new LangManager();
            fLangMan.LanguageChanged += LangChanged;

            fLMSettings = new LMSettings();
            LoadSettings(Path.Combine(SysUtils.GetBinPath(), "appsettings.json"));

            fLMClient = new LMChatClient(fLMSettings);
            fLMClient.View = this;
            fLMClient.MCPServer = context.MCPServer as MCPServer;
            fLMClient.HistoryStorage.BasePath = Path.Combine(context.DataPath, "lmchat");

            //MCPController.SetLMChat(fLMClient);

            InitLayout();
            InitChatWebpage();
            LoadModelsAsync().ConfigureAwait(false);
            LoadSessionsAsync();

            fLangMan.LoadFromFile(Path.Combine(SysUtils.GetBinPath(), "ZAssistant.rus"), null);
        }

        protected override void OnClosed(EventArgs e)
        {
            fLangMan.LanguageChanged -= LangChanged;
            SaveSettings(Path.Combine(SysUtils.GetBinPath(), "appsettings.json"));
            base.OnClosed(e);
        }

        #region Design

        private void LangChanged()
        {
            Title = fLangMan.LS(LSID.Title);
            fSendButton.Text = fLangMan.LS(LSID.Send);
            fStopButton.Text = fLangMan.LS(LSID.Stop);
            fRenameSessionButton.Text = fLangMan.LS(LSID.RenameSession);
            fSettingsButton.Text = "⚙ " + fLangMan.LS(LSID.Settings);
            fNewSessionButton.Text = fLangMan.LS(LSID.NewSession);
            fSessionActionsButton.Text = "✎ " + fLangMan.LS(LSID.Session);
            fModelLabel.Text = fLangMan.LS(LSID.Model);
            fSessionLabel.Text = fLangMan.LS(LSID.Session);
        }

        private WebView fWebView;
        private TextArea fInputArea;
        private Button fSendButton;
        private Button fStopButton;
        private DropDown fModelDropDown;
        private DropDown fSessionDropDown;
        private ButtonMenuItem fRenameSessionButton;
        private Button fSettingsButton;
        private ButtonMenuItem fNewSessionButton;
        private Button fSessionActionsButton;
        private Label fModelLabel;
        private Label fSessionLabel;
        private Button fAddTextFileButton;
        private Button fAddImageFileButton;

        private void InitLayout()
        {
            ClientSize = new Size(800, 600);
            MinimumSize = new Size(500, 400);

            fWebView = new WebView();
            fInputArea = new TextArea { Wrap = true };
            fSendButton = new Button { };
            fStopButton = new Button { Enabled = false };
            fModelDropDown = new DropDown();
            fSessionDropDown = new DropDown();
            fRenameSessionButton = new ButtonMenuItem { };
            fSettingsButton = new Button { };
            fNewSessionButton = new ButtonMenuItem { };
            fSessionActionsButton = new Button { };
            fModelLabel = new Label { };
            fSessionLabel = new Label { };
            fAddTextFileButton = new Button { Text = "📄" };
            fAddImageFileButton = new Button { Text = "🖼️" };

            var topPanel = new StackLayout {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Padding = new Padding(10, 5),
                Items = { fSettingsButton, fModelLabel, fModelDropDown, fSessionLabel, fSessionDropDown, fSessionActionsButton }
            };

            fInputArea.Height = 80;
            var bottomGrid = new StackLayout {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Padding = new Padding(4, 4),
                Items = {
                    new StackLayout { Orientation = Orientation.Vertical, Spacing = 4, Items = { fAddTextFileButton, fAddImageFileButton } },
                    new StackLayoutItem(fInputArea, true),
                    new StackLayout { Orientation = Orientation.Vertical, Spacing = 4, Items = { fSendButton, fStopButton } }
                }
            };

            Content = new TableLayout {
                Rows = { topPanel, new TableRow(fWebView) { ScaleHeight = true }, bottomGrid }
            };

            fSendButton.Click += async (s, e) => await SendMessageAsync();
            fStopButton.Click += (s, e) => StopMessage();
            fSettingsButton.Click += (s, e) => ShowSettingsDialog();
            fNewSessionButton.Click += (s, e) => NewSession();
            fRenameSessionButton.Click += async (s, e) => await RenameSession();
            fSessionDropDown.SelectedIndexChanged += async (s, e) => await LoadSelectedSession();
            fAddTextFileButton.Click += async (s, e) => await AddTextFileAsync();
            fAddImageFileButton.Click += async (s, e) => await AddImageFileAsync();

            fSessionActionsButton.ContextMenu = new ContextMenu {
                Items = {
                    fRenameSessionButton,
                    fNewSessionButton,
                }
            };
            fSessionActionsButton.Click += (s, e) => {
                var ctxMenu = (s as Button).ContextMenu;
                var buttonRect = (s as Button).Bounds;
                ctxMenu.Show(fSessionActionsButton, buttonRect.BottomLeft);
            };

            fModelDropDown.SelectedIndexChanged += (s, e) => {
                object selectedModel = fModelDropDown.SelectedValue;
                fLMSettings.ModelId = selectedModel.ToString();
            };
        }

        private async Task RenameSession()
        {
            if (fSessionDropDown.SelectedKey == null) return;

            var sessionId = fSessionDropDown.SelectedKey.ToString();
            var newName = await GetInput(this, fLangMan.LS(LSID.RenameSession), "");

            if (!string.IsNullOrEmpty(newName)) {
                fLMClient.HistoryStorage.RenameSession(sessionId, newName);
                LoadSessionsAsync();
            }
        }

        public static async Task<string> GetInput(object owner, string prompt, string value)
        {
            bool res = GKInputBox.QueryText(owner, "ZAssistant", prompt, ref value);
            string retVal = res && !string.IsNullOrEmpty(value) ? value : string.Empty;
            return await Task.FromResult(retVal);
        }

        private async Task LoadSelectedSession()
        {
            if (fSessionDropDown.SelectedKey == null) return;

            var sessionId = fSessionDropDown.SelectedKey.ToString();
            await fLMClient.HistoryStorage.LoadSessionAsync(sessionId);

            ExecuteWebScriptAsync("document.getElementById('chat').innerHTML = '';");
            foreach (var msg in fLMClient.HistoryStorage.CurrentHistory) {
                // Handle both string and array content
                if (msg.Content is string contentString) {
                    ShowMessage(contentString, msg.Role);
                }
                // For array content (files/images), show a simple message
                else if (msg.Content != null) {
                    ShowMessage("[File/Image content]", msg.Role);
                }
            }
        }

        private async void LoadSessionsAsync()
        {
            try {
                var sessions = await Task.Run(() => fLMClient.HistoryStorage.Sessions.Values
                    .OrderByDescending(s => s.StartDate)
                    .ToList());

                Application.Instance.AsyncInvoke(() => {
                    fSessionDropDown.Items.Clear();
                    foreach (var session in sessions) {
                        fSessionDropDown.Items.Add(session.Title, session.Id);
                    }
                    if (fSessionDropDown.Items.Count > 0)
                        fSessionDropDown.SelectedIndex = 0;
                });
            } catch (Exception ex) {
                Application.Instance.AsyncInvoke(() => MessageBox.Show(this, $"Error loading sessions: {ex.Message}"));
            }
        }

        /// <summary>
        /// Start a new session.
        /// </summary>
        private void NewSession()
        {
            fLMClient.NewSession();
            LoadSessionsAsync();

            // Clear the web view chat display
            ExecuteWebScriptAsync("document.getElementById('chat').innerHTML = '';");
        }

        /// <summary>
        /// Stop the current request.
        /// </summary>
        private void StopMessage()
        {
            fLMClient.CancelRequest();
            fStopButton.Enabled = false;
            fSendButton.Enabled = true;
        }

        private void LoadSettings(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try {
                string jsonContent = File.ReadAllText(filePath);
                var appSettings = JsonSerializer.Deserialize<AppSettings>(jsonContent);
                fLMSettings.Assign(appSettings.Assistant);
            } catch {
            }
        }

        private void SaveSettings(string filePath)
        {
            AppSettings appSettings;
            if (File.Exists(filePath)) {
                string jsonContentPrev = File.ReadAllText(filePath);
                appSettings = JsonSerializer.Deserialize<AppSettings>(jsonContentPrev);
            } else {
                appSettings = new AppSettings();
            }
            appSettings.Assistant = fLMSettings;

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonContent = JsonSerializer.Serialize(appSettings, options);

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, jsonContent);
        }

        private void ShowSettingsDialog()
        {
            var dialog = new Dialog();
            dialog.Title = fLangMan.LS(LSID.Settings);
            dialog.ClientSize = new Size(480, 600);

            // Create controls for parameters
            var apiAddressLabel = new Label { Text = fLangMan.LS(LSID.APIAddress) };
            var apiAddressTextBox = new TextBox { Text = fLMSettings.APIAddress };

            var apiKeyLabel = new Label { Text = fLangMan.LS(LSID.APIKey) };
            var apiKeyTextBox = new TextBox { Text = fLMSettings.APIKey };

            var temperatureLabel = new Label { Text = fLangMan.LS(LSID.Temperature) };
            var temperatureSlider = new Slider { MinValue = 0, MaxValue = 100, Value = (int)(fLMSettings.Temperature * 100) };
            var temperatureValueLabel = new Label { Text = fLMSettings.Temperature.ToString("F2") };

            var topPLabel = new Label { Text = fLangMan.LS(LSID.TopP) };
            var topPSlider = new Slider { MinValue = 0, MaxValue = 100, Value = (int)(fLMSettings.TopP * 100) };
            var topPValueLabel = new Label { Text = fLMSettings.TopP.ToString("F2") };

            var presenceLabel = new Label { Text = fLangMan.LS(LSID.PresencePenalty) };
            var presenceSlider = new Slider { MinValue = -200, MaxValue = 200, Value = (int)(fLMSettings.PresencePenalty * 100) };
            var presenceValueLabel = new Label { Text = fLMSettings.PresencePenalty.ToString("F2") };

            var frequencyLabel = new Label { Text = fLangMan.LS(LSID.FrequencyPenalty) };
            var frequencySlider = new Slider { MinValue = -200, MaxValue = 200, Value = (int)(fLMSettings.FrequencyPenalty * 100) };
            var frequencyValueLabel = new Label { Text = fLMSettings.FrequencyPenalty.ToString("F2") };

            var maxTokensLabel = new Label { Text = fLangMan.LS(LSID.MaxTokens) };
            var maxTokensNumeric = new NumericStepper { MinValue = 1, MaxValue = 8192, Value = fLMSettings.MaxTokens };

            var streamModeCheckBox = new CheckBox { Text = fLangMan.LS(LSID.StreamMode), Checked = fLMSettings.StreamMode };

            var systemPromptLabel = new Label { Text = fLangMan.LS(LSID.SystemPrompt) };
            var systemPromptTextArea = new TextArea {
                Text = fLMClient.SystemPrompt,
                Height = 100,
                Wrap = true
            };

            // Event handlers for updating values
            apiAddressTextBox.TextChanged += (s, e) => {
                fLMSettings.APIAddress = apiAddressTextBox.Text;
            };

            apiKeyTextBox.TextChanged += (s, e) => {
                fLMSettings.APIKey = apiKeyTextBox.Text;
            };

            temperatureSlider.ValueChanged += (s, e) => {
                fLMSettings.Temperature = temperatureSlider.Value / 100.0;
                temperatureValueLabel.Text = fLMSettings.Temperature.ToString("F2");
            };

            topPSlider.ValueChanged += (s, e) => {
                fLMSettings.TopP = topPSlider.Value / 100.0;
                topPValueLabel.Text = fLMSettings.TopP.ToString("F2");
            };

            presenceSlider.ValueChanged += (s, e) => {
                fLMSettings.PresencePenalty = presenceSlider.Value / 100.0;
                presenceValueLabel.Text = fLMSettings.PresencePenalty.ToString("F2");
            };

            frequencySlider.ValueChanged += (s, e) => {
                fLMSettings.FrequencyPenalty = frequencySlider.Value / 100.0;
                frequencyValueLabel.Text = fLMSettings.FrequencyPenalty.ToString("F2");
            };

            maxTokensNumeric.ValueChanged += (s, e) => {
                fLMSettings.MaxTokens = (int)maxTokensNumeric.Value;
            };

            streamModeCheckBox.CheckedChanged += (s, e) => {
                fLMSettings.StreamMode = streamModeCheckBox.Checked ?? false;
            };

            systemPromptTextArea.TextChanged += (s, e) => {
                fLMClient.SystemPrompt = systemPromptTextArea.Text;
            };

            var okButton = new Button { Text = "OK" };
            okButton.Click += async (s, e) => {
                dialog.Close();
                await LoadModelsAsync();
            };

            var tableLayout = new TableLayout {
                Spacing = new Size(5, 5),
                Padding = new Padding(10),
                Rows = {
                    new TableLayout {
                        Spacing = new Size(5, 5),
                        Rows = {
                            new TableRow(apiAddressLabel, apiAddressTextBox),
                            new TableRow(apiKeyLabel, apiKeyTextBox),
                        }
                    },
                    new TableLayout {
                        Spacing = new Size(5, 5),
                        Rows = {
                            new TableRow(temperatureLabel, temperatureSlider, temperatureValueLabel),
                            new TableRow(topPLabel, topPSlider, topPValueLabel),
                            new TableRow(presenceLabel, presenceSlider, presenceValueLabel),
                            new TableRow(frequencyLabel, frequencySlider, frequencyValueLabel),
                            new TableRow(maxTokensLabel, maxTokensNumeric, null),
                        }
                    },
                    new TableRow(streamModeCheckBox),
                    new TableRow(systemPromptLabel),
                    new TableRow(systemPromptTextArea) { ScaleHeight = true },
                    new StackLayout { Orientation = Orientation.Horizontal, Items = { new StackLayoutItem(null, true), okButton } }
                }
            };

            dialog.Content = tableLayout;
            dialog.ShowModal(this);
        }

        private void InitChatWebpage()
        {
            string baseHtml = @"
            <!DOCTYPE html>
            <html>
            <head>
                <script>
                </script>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 15px; background: #f9f9f9; color: #333; }
                    .chat-container { display: flex; flex-direction: column; gap: 12px; width: 100%; }
                    .chat-container > * + * { margin-top: 12px; }
                    .chat-container::after { content: ''; display: table; clear: both; }
                    .msg { padding: 10px 14px; border-radius: 8px; max-width: 85%; line-height: 1.5; word-wrap: break-word; box-sizing: border-box; }
                    .user { background: #00a8f4; color: white; align-self: flex-end; }
                    .assistant { background: #eaeaea; color: #111; align-self: flex-start; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
                    .system { background: #f0e1fc; color: #111; align-self: flex-start; }
                    .tool { background: #ffecb8; color: #111; align-self: flex-start; }
                    .reasoning { font-style: italic; color: #666; background: #f0f0f0; border-left: 3px solid #999; padding: 5px 10px; margin-bottom: 8px; font-size: 0.9em; }
                    table { border-collapse: collapse; margin: 10px 0; width: 100%; font-size: 0.95em; }
                    th, td { border: 1px solid #ccc; padding: 6px 10px; text-align: left; }
                    th { background-color: #ddd; }
                    p { margin: 4px 0; }
                    .streaming { background: #fff9c4; color: #333; }
                </style>
            </head>
            <body>
                <div class='chat-container' id='chat'></div>
            </body>
            </html>";

            fWebView.LoadHtml(baseHtml);
        }

        private void ExecuteWebScriptAsync(string script)
        {
            Application.Instance.AsyncInvoke(() => {
                fWebView.ExecuteScriptAsync(script);
            });
        }

        #endregion

        public void ShowMessage(string msg, string role)
        {
            if (string.IsNullOrEmpty(msg) || msg == "\"\"") return;

            string processedMsg = UIHelper.ConvertMarkdownToHtml(msg);
            // Escape content for JavaScript
            string escapedContent = processedMsg.Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
            ExecuteWebScriptAsync($@"document.getElementById('chat').insertAdjacentHTML('beforeend', '<div class=""msg {role}"">{escapedContent}</div>'); window.scrollTo(0, document.body.scrollHeight);");
        }

        void ILMChatView.StartStreamingMessage(int requestId)
        {
            ExecuteWebScriptAsync($@"document.getElementById('chat').insertAdjacentHTML('beforeend', '<div id=""msg_{requestId}"" class=""msg assistant streaming"">...</div>'); window.scrollTo(0, document.body.scrollHeight);");
        }

        void ILMChatView.UpdateStreamingMessage(int requestId, string content)
        {
            string processedMsg = UIHelper.ConvertMarkdownToHtml(content);
            // Escape content for JavaScript
            string escapedContent = processedMsg.Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
            ExecuteWebScriptAsync($@"document.getElementById('msg_{requestId}').innerHTML = '{escapedContent}'; window.scrollTo(0, document.body.scrollHeight);");
        }

        void ILMChatView.FinalizeStreamingMessage(int requestId)
        {
            ExecuteWebScriptAsync($@"document.getElementById('msg_{requestId}').className = 'msg assistant';");
        }

        private async Task LoadModelsAsync()
        {
            try {
                var models = await fLMClient.LoadModelsAsync();

                Application.Instance.AsyncInvoke(() => {
                    fModelDropDown.Items.Clear();
                    foreach (var m in models) {
                        fModelDropDown.Items.Add(m.Id);
                    }
                    if (fModelDropDown.Items.Count > 0) fModelDropDown.SelectedIndex = 0;
                });
            } catch (Exception ex) {
                Application.Instance.AsyncInvoke(() => MessageBox.Show(this, $"Error loading models: {ex.Message}"));
            }
        }

        private async Task AddTextFileAsync()
        {
            var openFileDialog = new OpenFileDialog {
                Title = "Select Text File",
                Filters = { new FileFilter("Markdown Files", ".md"), new FileFilter("Text Files", ".txt"), new FileFilter("All Files", ".*") }
            };

            if (openFileDialog.ShowDialog(this) == DialogResult.Ok) {
                try {
                    string filePath = openFileDialog.FileName;
                    string fileContent = File.ReadAllText(filePath);

                    // Add to chat history as a user message with file content
                    var contentItems = new List<ZLMKit.Protocols.ContentItem>
                    {
                        new ZLMKit.Protocols.ContentItem
                        {
                            Type = "text",
                            Text = $"File: {Path.GetFileName(filePath)}\n\n{fileContent}"
                        }
                    };

                    await fLMClient.AddHistory("user", contentItems);
                    ShowMessage($"Added text file: {Path.GetFileName(filePath)}", "user");
                } catch (Exception ex) {
                    MessageBox.Show(this, $"Error reading file: {ex.Message}");
                }
            }
        }

        private async Task AddImageFileAsync()
        {
            var openFileDialog = new OpenFileDialog {
                Title = "Select Image File",
                Filters = {
                    new FileFilter("Image Files", ".png", ".jpg", ".jpeg", ".gif", ".webp"),
                    new FileFilter("All Files", ".*")
                }
            };

            if (openFileDialog.ShowDialog(this) == DialogResult.Ok) {
                try {
                    string filePath = openFileDialog.FileName;
                    string base64Image = Convert.ToBase64String(File.ReadAllBytes(filePath));
                    string mimeType = GetMimeType(filePath);
                    string dataUrl = $"data:{mimeType};base64,{base64Image}";

                    // Add to chat history as a user message with image
                    var contentItems = new List<ZLMKit.Protocols.ContentItem>
                    {
                        new ZLMKit.Protocols.ContentItem
                        {
                            Type = "text",
                            Text = $"Image file: {Path.GetFileName(filePath)}"
                        },
                        new ZLMKit.Protocols.ContentItem
                        {
                            Type = "image_url",
                            ImageUrl = new ZLMKit.Protocols.ImageUrl
                            {
                                Url = dataUrl
                            }
                        }
                    };

                    await fLMClient.AddHistory("user", contentItems);
                    ShowMessage($"Added image file: {Path.GetFileName(filePath)}", "user");
                } catch (Exception ex) {
                    MessageBox.Show(this, $"Error reading image file: {ex.Message}");
                }
            }
        }

        private static string GetMimeType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        private async Task SendMessageAsync()
        {
            string userText = fInputArea.Text?.Trim();
            if (string.IsNullOrEmpty(userText) || string.IsNullOrEmpty(fLMSettings.ModelId)) return;

            fInputArea.Text = string.Empty;
            fSendButton.Enabled = false;
            fStopButton.Enabled = true;

            await fLMClient.AddHistory("user", userText);
            ShowMessage(userText, "user");

            try {
                await fLMClient.SendMessageAsync();
            } catch (Exception ex) {
                ShowMessage($"Error: {JsonSerializer.Serialize(ex.Message)}", "assistant");
            } finally {
                Application.Instance.AsyncInvoke(() => {
                    fSendButton.Enabled = true;
                    fStopButton.Enabled = false;
                });
            }
        }
    }
}
