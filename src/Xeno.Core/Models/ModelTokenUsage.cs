namespace Xeno.Core.Models
{
    /// <summary>
    /// Token usage totals for one model.
    /// </summary>
    public class ModelTokenUsage
    {
        /// <summary>
        /// Total tokens reported for the model.
        /// </summary>
        public long TotalTokens { get; set; } = 0;

        /// <summary>
        /// Prompt/input tokens reported for the model.
        /// </summary>
        public long PromptTokens { get; set; } = 0;

        /// <summary>
        /// Output/completion tokens reported for the model.
        /// </summary>
        public long OutputTokens { get; set; } = 0;
    }
}
