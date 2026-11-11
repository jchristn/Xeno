namespace Xeno.Client.Tools.Workspace.Processes
{
    using System;
    using System.Text;

    /// <summary>
    /// Captures process output line by line, keeping the first characters up to a limit and counting the rest.
    /// Thread safe.
    /// </summary>
    internal sealed class BoundedOutput
    {
        #region Private-Members

        private readonly object _Sync = new object();
        private readonly StringBuilder _Text = new StringBuilder();
        private readonly int _Limit;
        private long _Total = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the buffer.
        /// </summary>
        /// <param name="limit">Most characters kept.</param>
        public BoundedOutput(int limit)
        {
            _Limit = Math.Max(1, limit);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a line.
        /// </summary>
        /// <param name="line">Line without its newline; null is ignored.</param>
        public void AppendLine(string? line)
        {
            if (line == null) return;
            lock (_Sync)
            {
                _Total += line.Length + 1;
                int room = _Limit - _Text.Length;
                if (room <= 0) return;
                if (line.Length + 1 <= room)
                {
                    _Text.Append(line).Append('\n');
                }
                else
                {
                    _Text.Append(line, 0, Math.Min(line.Length, room));
                }
            }
        }

        /// <summary>
        /// The kept text, with a note when output was dropped.
        /// </summary>
        /// <returns>Text.</returns>
        public override string ToString()
        {
            lock (_Sync)
            {
                if (_Total <= _Limit) return _Text.ToString();
                return _Text.ToString() + "\n[truncated: output exceeded " + _Limit + " characters; " + (_Total - _Text.Length) + " more were dropped]";
            }
        }

        #endregion
    }
}
