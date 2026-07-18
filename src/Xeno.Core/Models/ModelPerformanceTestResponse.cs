namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Dashboard response containing side-by-side model performance results.
    /// </summary>
    public class ModelPerformanceTestResponse
    {
        /// <summary>
        /// Performance test request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Model name that was tested.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Request type that was tested.
        /// </summary>
        public string RequestType { get; set; } = "chatCompletions";

        /// <summary>
        /// Number of samples requested per leg.
        /// </summary>
        public int RequestCount { get; set; } = 0;

        /// <summary>
        /// Client ID from which hairpin requests originated.
        /// </summary>
        public string SourceClientId { get; set; } = String.Empty;

        /// <summary>
        /// Source client name.
        /// </summary>
        public string? SourceClientName { get; set; } = null;

        /// <summary>
        /// Client ID that hosted the model.
        /// </summary>
        public string TargetClientId { get; set; } = String.Empty;

        /// <summary>
        /// Target client name.
        /// </summary>
        public string? TargetClientName { get; set; } = null;

        /// <summary>
        /// Direct native-provider results from the model-hosting client.
        /// </summary>
        public ModelPerformanceTestLegResult Native { get; set; } = new ModelPerformanceTestLegResult();

        /// <summary>
        /// Hairpin relay results from source client through Xeno server to target client.
        /// </summary>
        public ModelPerformanceTestLegResult Hairpin { get; set; } = new ModelPerformanceTestLegResult();
    }
}
