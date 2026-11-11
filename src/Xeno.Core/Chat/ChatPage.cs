namespace Xeno.Core.Chat
{
    using System.Collections.Generic;

    /// <summary>
    /// One page of query results.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    public sealed class ChatPage<T>
    {
        /// <summary>
        /// Items on the page.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Items matching the filters across all pages.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Page number, starting at 1.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 50;
    }
}
