namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Turns HTML into readable plain text without running scripts: drops scripts, styles, and comments, breaks lines at
    /// block elements, removes tags, decodes entities, and collapses whitespace.
    /// </summary>
    internal static class HtmlText
    {
        #region Private-Members

        private static readonly TimeSpan _Timeout = TimeSpan.FromSeconds(5);
        private const RegexOptions _Options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled;
        private static readonly Regex _Title = new Regex(@"<title\b[^>]*>(.*?)</title\s*>", _Options, _Timeout);
        private static readonly Regex _Comments = new Regex(@"<!--.*?-->", _Options, _Timeout);
        private static readonly Regex _Hidden = new Regex(@"<(script|style|noscript|template|svg|head|iframe|object)\b[^>]*>.*?</\1\s*>", _Options, _Timeout);
        private static readonly Regex _LineBreaks = new Regex(@"<br\s*/?>|</?(p|div|section|article|header|footer|nav|aside|main|h[1-6]|li|ul|ol|tr|table|thead|tbody|pre|blockquote|hr|form|dd|dt|dl|figure|figcaption|address|details|summary)\b[^>]*>", _Options, _Timeout);
        private static readonly Regex _Cells = new Regex(@"</?(td|th)\b[^>]*>", _Options, _Timeout);
        private static readonly Regex _Tags = new Regex(@"<[^>]*>", _Options, _Timeout);
        private static readonly Regex _Spaces = new Regex(@"[ \t\f\v ]+", RegexOptions.Compiled | RegexOptions.CultureInvariant, _Timeout);
        private static readonly Regex _BlankLines = new Regex(@"\n{3,}", RegexOptions.Compiled | RegexOptions.CultureInvariant, _Timeout);

        #endregion

        #region Public-Methods

        /// <summary>
        /// The document title, or empty.
        /// </summary>
        /// <param name="html">HTML.</param>
        /// <returns>Title.</returns>
        public static string Title(string html)
        {
            try
            {
                Match match = _Title.Match(html);
                if (!match.Success) return String.Empty;
                return Collapse(WebUtility.HtmlDecode(_Tags.Replace(match.Groups[1].Value, " "))).Replace('\n', ' ').Trim();
            }
            catch (RegexMatchTimeoutException)
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Visible text of an HTML document.
        /// </summary>
        /// <param name="html">HTML.</param>
        /// <returns>Plain text.</returns>
        public static string ToText(string html)
        {
            string text;
            try
            {
                text = _Comments.Replace(html, " ");
                text = _Hidden.Replace(text, " ");
                text = _LineBreaks.Replace(text, "\n");
                text = _Cells.Replace(text, "\t");
                text = _Tags.Replace(text, " ");
            }
            catch (RegexMatchTimeoutException)
            {
                text = html;
            }

            return Collapse(WebUtility.HtmlDecode(text));
        }

        #endregion

        #region Private-Methods

        private static string Collapse(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            StringBuilder builder = new StringBuilder();
            foreach (string line in lines)
            {
                builder.Append(_Spaces.Replace(line, " ").Trim()).Append('\n');
            }

            return _BlankLines.Replace(builder.ToString(), "\n\n").Trim();
        }

        #endregion
    }
}
