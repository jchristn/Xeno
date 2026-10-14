namespace Test.Shared
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui;

    /// <summary>
    /// Runs the Xeno terminal app on a headless TUIKit backend against a temporary client home, so tests can press
    /// keys and assert on rendered frames.
    /// </summary>
    internal sealed class TuiHarness : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Headless backend.
        /// </summary>
        public HeadlessBackend Backend { get; }

        /// <summary>
        /// TUIKit host.
        /// </summary>
        public TuiApplication App { get; }

        /// <summary>
        /// Terminal app.
        /// </summary>
        public XenoTuiApp Tui { get; }

        /// <summary>
        /// Workspace.
        /// </summary>
        public XenoWorkspace Workspace { get; }

        #endregion

        #region Private-Members

        private readonly ClientHomeScope _Home;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the terminal app headlessly.
        /// </summary>
        /// <param name="width">Columns.</param>
        /// <param name="height">Rows.</param>
        /// <param name="page">Start page.</param>
        /// <param name="server">Canned Xeno server responses, or null for none.</param>
        public TuiHarness(int width, int height, XenoPageEnum page, StubHttpHandler? server = null)
        {
            _Home = new ClientHomeScope();
            Backend = new HeadlessBackend(width, height) { ClaimsTerminal = false };
            App = new TuiApplication(Backend);
            Workspace = new XenoWorkspace(new XenoServerClient(server ?? new StubHttpHandler()), () => null);
            Tui = new XenoTuiApp(App, new TuiStartOptions { StartPage = page, Live = false, PreferencesPath = Path.Combine(_Home.Directory, "frontend.json") }, Workspace);
            App.Start();
            Tui.Start();
            Settle();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Pump input and posted work until background loads finish.
        /// </summary>
        /// <param name="timeoutMs">Maximum time to wait.</param>
        public void Settle(int timeoutMs = 5000)
        {
            Stopwatch watch = Stopwatch.StartNew();
            int quiet = 0;
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                App.PumpInputOnce();
                App.RenderOnce();
                bool idle = Tui.Context.Pending == 0 && Tui.StartupTask.IsCompleted;
                quiet = idle ? quiet + 1 : 0;
                if (quiet >= 5) return;
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// Press a key chord, for example "alt+3", "ctrl+k", "f10", or "enter".
        /// </summary>
        /// <param name="chord">Chord.</param>
        public void Press(string chord)
        {
            Backend.FeedKey(chord);
            Settle();
        }

        /// <summary>
        /// Type text.
        /// </summary>
        /// <param name="text">Text.</param>
        public void Type(string text)
        {
            Backend.FeedInput(text);
            Settle();
        }

        /// <summary>
        /// Render and return the screen as text.
        /// </summary>
        /// <returns>Screen text.</returns>
        /// <summary>
        /// Left-click a cell, as a terminal would send it, and let the app settle.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        public void Click(int x, int y)
        {
            App.RenderOnce();
            Backend.FeedClick(x, y);
            Settle();
        }

        /// <summary>
        /// Double-click a cell and let the app settle.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        public void DoubleClick(int x, int y)
        {
            App.RenderOnce();
            Backend.FeedDoubleClick(x, y);
            Settle();
        }

        /// <summary>
        /// Turn the mouse wheel over a cell and let the app settle.
        /// </summary>
        /// <param name="x">Column.</param>
        /// <param name="y">Row.</param>
        /// <param name="down">True to scroll down.</param>
        public void Wheel(int x, int y, bool down)
        {
            App.RenderOnce();
            Backend.FeedWheel(x, y, down ? 1 : -1);
            Settle();
        }

        /// <summary>
        /// Click the first occurrence of text on screen, searching rows from the top (or from a starting row).
        /// </summary>
        /// <param name="text">Text to click.</param>
        /// <param name="fromRow">First row to search.</param>
        /// <exception cref="InvalidOperationException">The text is not on screen.</exception>
        public void ClickText(string text, int fromRow = 0)
        {
            TUIKit.Point at = Find(text, fromRow) ?? throw new InvalidOperationException("'" + text + "' is not on screen:" + Environment.NewLine + Screen());
            Click(at.X, at.Y);
        }

        /// <summary>
        /// Position of the first occurrence of text on screen, or null.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="fromRow">First row to search.</param>
        /// <returns>Column and row of the first character, or null.</returns>
        public TUIKit.Point? Find(string text, int fromRow = 0)
        {
            string[] rows = Screen().Replace("\r", String.Empty).Split('\n');
            for (int y = Math.Max(0, fromRow); y < rows.Length; y++)
            {
                int x = rows[y].IndexOf(text, StringComparison.Ordinal);
                if (x >= 0) return new TUIKit.Point(x, y);
            }

            return null;
        }

        public string Screen()
        {
            App.RenderOnce();
            CellBuffer? frame = App.CaptureFrame();
            return frame == null ? String.Empty : Snapshot.ToText(frame);
        }

        /// <summary>
        /// Stop the app and remove the temporary client home.
        /// </summary>
        public void Dispose()
        {
            Tui.Dispose();
            App.Dispose();
            Workspace.Dispose();
            Backend.Dispose();
            _Home.Dispose();
        }

        #endregion
    }
}
