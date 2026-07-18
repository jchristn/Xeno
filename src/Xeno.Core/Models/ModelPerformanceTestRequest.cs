namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Dashboard request to compare native model performance against Xeno hairpin relay performance.
    /// </summary>
    public class ModelPerformanceTestRequest
    {
        /// <summary>
        /// Model name to test.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Client ID from which hairpin requests should originate.
        /// </summary>
        public string SourceClientId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID that hosts the model and processes requests.
        /// </summary>
        public string TargetClientId { get; set; } = String.Empty;

        /// <summary>
        /// Optional provider source name on the target client.
        /// </summary>
        public string? TargetSource { get; set; } = null;

        /// <summary>
        /// Request type: chatCompletions, completions, or embeddings.
        /// </summary>
        public string RequestType { get; set; } = "chatCompletions";

        /// <summary>
        /// Number of requests to run. The server clamps values to 1 through 10.
        /// </summary>
        public int RequestCount { get; set; } = 5;
    }
}
