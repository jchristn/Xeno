namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Time one stage took in one request or turn.
    /// </summary>
    public sealed class UsageStageDuration
    {
        /// <summary>
        /// Stage.
        /// </summary>
        public UsageStageEnum Stage { get; set; } = UsageStageEnum.Other;

        /// <summary>
        /// Milliseconds, never negative.
        /// </summary>
        public double Milliseconds { get; set; } = 0;
    }
}
