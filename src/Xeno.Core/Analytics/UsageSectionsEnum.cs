namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// Sections of a usage analytics response. Leaving a section out skips its work.
    /// </summary>
    [Flags]
    public enum UsageSectionsEnum
    {
        /// <summary>
        /// Nothing.
        /// </summary>
        None = 0,

        /// <summary>
        /// Range totals for the KPI tiles.
        /// </summary>
        Totals = 1,

        /// <summary>
        /// Requests by outcome per bucket.
        /// </summary>
        Outcome = 2,

        /// <summary>
        /// Total latency distribution per bucket.
        /// </summary>
        Latency = 4,

        /// <summary>
        /// Time to first token distribution per bucket.
        /// </summary>
        TimeToFirstToken = 8,

        /// <summary>
        /// Streaming time distribution per bucket.
        /// </summary>
        Streaming = 16,

        /// <summary>
        /// Tokens per second distribution per bucket.
        /// </summary>
        Throughput = 32,

        /// <summary>
        /// Tokens by type per bucket.
        /// </summary>
        Tokens = 64,

        /// <summary>
        /// Activity and totals by group.
        /// </summary>
        Groups = 128,

        /// <summary>
        /// Token activity heatmap.
        /// </summary>
        Heatmap = 256,

        /// <summary>
        /// Timing by stage.
        /// </summary>
        Stages = 512,

        /// <summary>
        /// Thumbs up and down per bucket (chat).
        /// </summary>
        Feedback = 1024,

        /// <summary>
        /// Tool calls per bucket (chat).
        /// </summary>
        Tools = 2048,

        /// <summary>
        /// Every section.
        /// </summary>
        All = 4095
    }
}
