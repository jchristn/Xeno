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
