namespace Xeno.Client.Presentation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Commands available from the menus of the desktop app, the terminal app, and the tray agent. Each front end
    /// executes the subset its menus contain.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum XenoCommandEnum
    {
        /// <summary>Show the About window.</summary>
        About,
        /// <summary>Open the Settings page.</summary>
        Settings,
        /// <summary>Quit the application.</summary>
        Quit,
        /// <summary>Show the desktop window (tray).</summary>
        ShowWindow,
        /// <summary>Close the current window.</summary>
        CloseWindow,
        /// <summary>Refresh the current page.</summary>
        Refresh,
        /// <summary>Add a provider endpoint.</summary>
        NewProvider,
        /// <summary>Add a model to a provider.</summary>
        NewModel,
        /// <summary>Open client.json in the default editor.</summary>
        OpenSettingsFile,
        /// <summary>Reveal the client home folder.</summary>
        RevealDataFolder,
        /// <summary>Go to the Overview page.</summary>
        GoOverview,
        /// <summary>Go to the Models page.</summary>
        GoModels,
        /// <summary>Go to the Providers page.</summary>
        GoProviders,
        /// <summary>Go to the Requests page.</summary>
        GoRequests,
        /// <summary>Go to the Logs page.</summary>
        GoLogs,
        /// <summary>Go to the Host telemetry page.</summary>
        GoHostTelemetry,
        /// <summary>Go to the Provider telemetry page.</summary>
        GoProviderTelemetry,
        /// <summary>Go to the Server and Account page.</summary>
        GoServer,
        /// <summary>Go to the Settings page.</summary>
        GoSettings,
        /// <summary>Follow the system light or dark appearance.</summary>
        AppearanceSystem,
        /// <summary>Use the light appearance.</summary>
        AppearanceLight,
        /// <summary>Use the dark appearance.</summary>
        AppearanceDark,
        /// <summary>Start the client worker.</summary>
        StartClient,
        /// <summary>Stop the client worker.</summary>
        StopClient,
        /// <summary>Restart the client worker.</summary>
        RestartClient,
        /// <summary>Re-advertise providers and models to the Xeno server.</summary>
        ReloadConfiguration,
        /// <summary>Sign in to a Xeno server.</summary>
        SignIn,
        /// <summary>Ping the Xeno server.</summary>
        TestConnection,
        /// <summary>Open the local web dashboard.</summary>
        OpenWebDashboard,
        /// <summary>Open the desktop app.</summary>
        OpenDesktopApp,
        /// <summary>Open the terminal app in a new terminal window.</summary>
        OpenTerminalApp,
        /// <summary>Undo in the focused text field.</summary>
        Undo,
        /// <summary>Redo in the focused text field.</summary>
        Redo,
        /// <summary>Cut from the focused text field.</summary>
        Cut,
        /// <summary>Copy from the focused control.</summary>
        Copy,
        /// <summary>Paste into the focused text field.</summary>
        Paste,
        /// <summary>Select all in the focused text field.</summary>
        SelectAll,
        /// <summary>Minimize the window.</summary>
        MinimizeWindow,
        /// <summary>Zoom the window.</summary>
        ZoomWindow,
        /// <summary>Bring all windows to the front.</summary>
        BringAllToFront,
        /// <summary>Open the documentation.</summary>
        Documentation,
        /// <summary>Open the issue tracker.</summary>
        ReportIssue,
        /// <summary>Open the community Discord.</summary>
        Community,
        /// <summary>Show keyboard shortcuts.</summary>
        KeyboardShortcuts,
        /// <summary>Open the command palette.</summary>
        CommandPalette
    }
}
