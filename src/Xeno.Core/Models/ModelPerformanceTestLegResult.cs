namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Model performance results for one benchmark leg.
    /// </summary>
    public class ModelPerformanceTestLegResult
    {
        /// <summary>
        /// Leg name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Human-readable leg description.
        /// </summary>
        public string Description { get; set; } = String.Empty;

        /// <summary>
        /// Sample results.
        /// </summary>
        public List<ModelPerformanceTestSampleResult> Samples { get; set; } = new List<ModelPerformanceTestSampleResult>();

        /// <summary>
        /// Aggregate summary.
        /// </summary>
        public ModelPerformanceTestSummary Summary { get; set; } = new ModelPerformanceTestSummary();
    }
}
