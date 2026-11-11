namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Xeno.Core.Chat.Wire;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Reads an OpenAI-compatible /v1/chat/completions response as it arrives, streamed (server-sent events) or not
    /// (one JSON body), and collects the answer text, reasoning text, tool calls, token usage, and any error. Not
    /// thread safe; feed it from one reader.
    /// </summary>
    public sealed class ChatCompletionAccumulator
    {
        #region Public-Members

        /// <summary>
        /// Raised with each piece of answer text.
        /// </summary>
        public event Action<string>? ContentReceived;

        /// <summary>
        /// Raised with each piece of reasoning text.
        /// </summary>
        public event Action<string>? ThinkingReceived;

        /// <summary>
        /// Answer text so far.
        /// </summary>
        public string Content
        {
            get { return _Content.ToString(); }
        }

        /// <summary>
        /// Reasoning text so far, or null when there was none.
        /// </summary>
        public string? Thinking
        {
            get { return _Thinking.Length == 0 ? null : _Thinking.ToString(); }
        }

        /// <summary>
        /// Tool calls the model asked for, in order. Arguments are complete once the response ends.
        /// </summary>
        public IReadOnlyList<ChatCompletionToolCallDto> ToolCalls
        {
            get { return _ToolCalls.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList(); }
        }

        /// <summary>
        /// Prompt tokens, when the provider reported them.
        /// </summary>
        public int? PromptTokens { get; private set; } = null;

        /// <summary>
        /// Completion tokens, when the provider reported them.
        /// </summary>
        public int? CompletionTokens { get; private set; } = null;

        /// <summary>
        /// Reasoning tokens inside the completion tokens, when the provider reported them.
        /// </summary>
        public int? ReasoningTokens { get; private set; } = null;

        /// <summary>
        /// Prompt tokens served from the provider's cache, when the provider reported them.
        /// </summary>
        public int? CachedPromptTokens { get; private set; } = null;

        /// <summary>
        /// Finish reason of the last choice, or null.
        /// </summary>
        public string? FinishReason { get; private set; } = null;

        /// <summary>
        /// Error the provider reported in the body, or null.
        /// </summary>
        public string? Error { get; private set; } = null;

        /// <summary>
        /// True once the stream sent [DONE] or a finish reason, or a whole JSON body was read.
        /// </summary>
        public bool Finished { get; private set; } = false;

        /// <summary>
        /// True when any answer text, reasoning, or tool call arrived.
        /// </summary>
        public bool ReceivedOutput
        {
            get { return _Content.Length > 0 || _Thinking.Length > 0 || _ToolCalls.Count > 0; }
        }

        #endregion

        #region Private-Members

        private readonly StringBuilder _Content = new StringBuilder();
        private readonly StringBuilder _Thinking = new StringBuilder();
        private readonly SortedDictionary<int, ChatCompletionToolCallDto> _ToolCalls = new SortedDictionary<int, ChatCompletionToolCallDto>();
        private readonly StringBuilder _Pending = new StringBuilder();
        private readonly Decoder _Decoder = new UTF8Encoding(false).GetDecoder();
        private bool? _Streaming = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Feed response bytes as they arrive.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <param name="count">Number of bytes to read from the start of <paramref name="bytes"/>.</param>
        /// <exception cref="ArgumentNullException">bytes is null.</exception>
        public void Append(byte[] bytes, int count)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (count <= 0) return;
            char[] chars = new char[_Decoder.GetCharCount(bytes, 0, count)];
            int written = _Decoder.GetChars(bytes, 0, count, chars, 0);
            Append(new string(chars, 0, written));
        }

        /// <summary>
        /// Feed response text as it arrives.
        /// </summary>
        /// <param name="text">Text.</param>
        public void Append(string? text)
        {
            if (String.IsNullOrEmpty(text)) return;
            _Pending.Append(text);
            if (_Streaming == null)
            {
                string head = _Pending.ToString().TrimStart();
                if (head.Length == 0) return;
                _Streaming = !head.StartsWith("{", StringComparison.Ordinal);
            }

            if (_Streaming == true) DrainLines(false);
        }

        /// <summary>
        /// Mark the end of the response and process anything still buffered.
        /// </summary>
        public void Complete()
        {
            if (_Streaming == true)
            {
                DrainLines(true);
                return;
            }

            string body = _Pending.ToString().Trim();
            _Pending.Clear();
            if (body.Length == 0) return;
            ReadChunk(body);
            Finished = true;
        }

        #endregion

        #region Private-Methods

        private void DrainLines(bool final)
        {
            while (true)
            {
                string buffered = _Pending.ToString();
                int newline = buffered.IndexOf('\n');
                if (newline < 0)
                {
                    if (final && buffered.Trim().Length > 0)
                    {
                        _Pending.Clear();
                        ReadLine(buffered);
                    }

                    return;
                }

                string line = buffered.Substring(0, newline);
                _Pending.Remove(0, newline + 1);
                ReadLine(line);
            }
        }

        private void ReadLine(string raw)
        {
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith(":", StringComparison.Ordinal)) return;
            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase)) return;
            string data = line.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? line.Substring(5).Trim() : line;
            if (data.Length == 0) return;
            if (String.Equals(data, "[DONE]", StringComparison.Ordinal))
            {
                Finished = true;
                return;
            }

            ReadChunk(data);
        }

        private void ReadChunk(string json)
        {
            ChatCompletionChunkDto? chunk;
            try
            {
                chunk = JsonUtil.Deserialize<ChatCompletionChunkDto>(json);
            }
            catch (InvalidDataException)
            {
                // Some servers send {"error": "text"}; anything else that is not a chunk is ignored.
                if (Error == null && JsonUtil.TryDeserialize<ChatCompletionErrorTextDto>(json, out ChatCompletionErrorTextDto? text) && !String.IsNullOrWhiteSpace(text?.Error)) Error = TrimForError(text!.Error!);
                return;
            }

            if (chunk == null) return;
            if (chunk.Error != null) Error = chunk.Error.Message ?? chunk.Error.Type ?? "The provider reported an error.";
            if (chunk.Usage != null)
            {
                if (chunk.Usage.PromptTokens.HasValue) PromptTokens = chunk.Usage.PromptTokens;
                if (chunk.Usage.CompletionTokens.HasValue) CompletionTokens = chunk.Usage.CompletionTokens;
                if (chunk.Usage.CompletionDetails?.ReasoningTokens != null) ReasoningTokens = chunk.Usage.CompletionDetails.ReasoningTokens;
                if (chunk.Usage.PromptDetails?.CachedTokens != null) CachedPromptTokens = chunk.Usage.PromptDetails.CachedTokens;
            }

            if (chunk.Choices == null) return;
            foreach (ChatCompletionChoiceDto choice in chunk.Choices)
            {
                if (choice.Delta != null)
                {
                    AddThinking(choice.Delta.ReasoningContent ?? choice.Delta.Reasoning);
                    AddContent(choice.Delta.Content);
                    AddToolCalls(choice.Delta.ToolCalls);
                }

                if (choice.Message != null)
                {
                    AddThinking(choice.Message.ReasoningContent);
                    AddContent(choice.Message.Content);
                    AddToolCalls(choice.Message.ToolCalls);
                }

                if (!String.IsNullOrWhiteSpace(choice.FinishReason))
                {
                    FinishReason = choice.FinishReason;
                    Finished = true;
                }
            }
        }

        private void AddContent(string? text)
        {
            if (String.IsNullOrEmpty(text)) return;
            _Content.Append(text);
            ContentReceived?.Invoke(text);
        }

        private void AddThinking(string? text)
        {
            if (String.IsNullOrEmpty(text)) return;
            _Thinking.Append(text);
            ThinkingReceived?.Invoke(text);
        }

        private void AddToolCalls(List<ChatCompletionToolCallDto>? calls)
        {
            if (calls == null) return;
            foreach (ChatCompletionToolCallDto call in calls)
            {
                int index = call.Index ?? _ToolCalls.Count;
                if (!_ToolCalls.TryGetValue(index, out ChatCompletionToolCallDto? existing))
                {
                    existing = new ChatCompletionToolCallDto { Index = index, Function = new ChatCompletionFunctionCallDto { Name = String.Empty, Arguments = String.Empty } };
                    _ToolCalls[index] = existing;
                }

                if (!String.IsNullOrEmpty(call.Id)) existing.Id = call.Id;
                if (call.Function == null) continue;
                ChatCompletionFunctionCallDto target = existing.Function!;
                if (!String.IsNullOrEmpty(call.Function.Name)) target.Name = (target.Name ?? String.Empty).Length == 0 ? call.Function.Name : target.Name;
                if (!String.IsNullOrEmpty(call.Function.Arguments)) target.Arguments = (target.Arguments ?? String.Empty) + call.Function.Arguments;
            }
        }

        private static string TrimForError(string json)
        {
            return json.Length <= 500 ? json : json.Substring(0, 500);
        }

        #endregion
    }
}
