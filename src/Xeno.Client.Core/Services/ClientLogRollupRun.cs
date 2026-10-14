namespace Xeno.Client.Services
{
    using System;
    using System.Globalization;

    /// <summary>
    /// One message type within a run of rolled-up log lines (see <see cref="ClientLogRollup"/>).
    /// </summary>
    internal sealed class ClientLogRollupRun
    {
        #region Public-Members

        /// <summary>
        /// Message type.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Position of the latest line within the run, for ordering.
        /// </summary>
        public int LastPosition { get; private set; } = 0;

        #endregion

        #region Private-Members

        private readonly string _FirstLine;
        private string _LastLine;
        private int _Count = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start a run.
        /// </summary>
        /// <param name="message">Message type.</param>
        /// <param name="firstLine">First line of the run.</param>
        public ClientLogRollupRun(string message, string firstLine)
        {
            Message = message;
            _FirstLine = firstLine;
            _LastLine = firstLine;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Count a line.
        /// </summary>
        /// <param name="line">Line.</param>
        /// <param name="position">Position within the run.</param>
        public void Add(string line, int position)
        {
            _LastLine = line;
            _Count++;
            LastPosition = position;
        }

        /// <summary>
        /// The line that stands for the run: the line itself when it occurred once, otherwise the latest line with a
        /// count and the local time of the first.
        /// </summary>
        /// <returns>Line.</returns>
        public string Describe()
        {
            if (_Count <= 1) return _LastLine;
            int index = _LastLine.IndexOf(ClientLogRollup.ServerMessagePrefix, StringComparison.Ordinal);
            string since = ClientLogRollup.LocalTimeOfDay(_FirstLine) ?? "the first";
            return _LastLine.Substring(0, index) + ClientLogRollup.ServerMessagePrefix + Message + " (" + _Count.ToString(CultureInfo.InvariantCulture) + " since " + since + ")";
        }

        #endregion
    }
}
