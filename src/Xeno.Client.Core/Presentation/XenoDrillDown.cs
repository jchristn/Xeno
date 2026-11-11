namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Core.Analytics;

    /// <summary>
    /// Hands a time window from a Usage chart to the page that lists those requests or turns, in the desktop and
    /// terminal apps. The Usage page sets a request and navigates; the target page takes it on its next load and shows
    /// only that window until the user changes the range. Thread safe.
    /// </summary>
    public static class XenoDrillDown
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static XenoDrillDownRequest? _Pending = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// The page that lists a dataset's records.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <returns>Page.</returns>
        public static XenoPageEnum PageFor(UsageDatasetEnum dataset)
        {
            switch (dataset)
            {
                case UsageDatasetEnum.LocalEndpoint: return XenoPageEnum.LocalEndpoint;
                case UsageDatasetEnum.Chat: return XenoPageEnum.ChatHistory;
                default: return XenoPageEnum.Requests;
            }
        }

        /// <summary>
        /// Ask a page to show a window.
        /// </summary>
        /// <param name="page">Target page.</param>
        /// <param name="fromUtc">Start, UTC.</param>
        /// <param name="toUtc">End, UTC.</param>
        /// <param name="model">Only this model, or null.</param>
        /// <returns>The request.</returns>
        public static XenoDrillDownRequest Request(XenoPageEnum page, DateTime fromUtc, DateTime toUtc, string? model)
        {
            XenoDrillDownRequest request = new XenoDrillDownRequest { Page = page, FromUtc = fromUtc, ToUtc = toUtc, Model = String.IsNullOrWhiteSpace(model) ? null : model };
            lock (_Lock) _Pending = request;
            return request;
        }

        /// <summary>
        /// Take the pending request for a page, if there is one.
        /// </summary>
        /// <param name="page">Page.</param>
        /// <returns>The request, or null.</returns>
        public static XenoDrillDownRequest? Take(XenoPageEnum page)
        {
            lock (_Lock)
            {
                if (_Pending == null || _Pending.Page != page) return null;
                XenoDrillDownRequest request = _Pending;
                _Pending = null;
                return request;
            }
        }

        /// <summary>
        /// Describe a window in local time, for example "11-10 14:15 to 14:30, llama3".
        /// </summary>
        /// <param name="request">Request.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        public static string Describe(XenoDrillDownRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            DateTime from = DateTime.SpecifyKind(request.FromUtc, DateTimeKind.Utc).ToLocalTime();
            DateTime to = DateTime.SpecifyKind(request.ToUtc, DateTimeKind.Utc).ToLocalTime();
            string text = from.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + " to " + to.ToString(from.Date == to.Date ? "HH:mm" : "MM-dd HH:mm", CultureInfo.InvariantCulture);
            return request.Model == null ? text : text + ", " + request.Model;
        }

        #endregion
    }
}
