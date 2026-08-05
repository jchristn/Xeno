namespace Xeno.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using Xeno.Core.Models;

    /// <summary>
    /// A page of onboarding requests plus the total count matching the query.
    /// </summary>
    public class OnboardingRecordPage
    {
        /// <summary>
        /// Matching onboarding requests for the requested page.
        /// </summary>
        public List<OnboardingRequest> Items { get; set; } = new List<OnboardingRequest>();

        /// <summary>
        /// Total number of records matching the query, ignoring paging.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OnboardingRecordPage()
        {
        }
    }
}
