namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Net.Http;

    /// <summary>
    /// The web tools ported from Mux: web_retrieve and web_search. Both only read.
    /// </summary>
    public static class WebTools
    {
        #region Public-Methods

        /// <summary>
        /// Create the tools. They share HTTP clients, so create them once per host.
        /// </summary>
        /// <param name="handler">HTTP handler for every request (tests), or null for the real network. The caller owns it.</param>
        /// <returns>Tools.</returns>
        public static IReadOnlyList<IWorkspaceTool> Create(HttpMessageHandler? handler = null)
        {
            WebHttp http = new WebHttp(handler);
            return new List<IWorkspaceTool>
            {
                new WebRetrieveTool(http),
                new WebSearchTool(http)
            };
        }

        #endregion
    }
}
