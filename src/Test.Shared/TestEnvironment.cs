namespace Test.Shared
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Process-wide settings for every harness that loads these suites: commands and apps started by tests never launch
    /// the real tray agent or connect on their own. Child processes inherit the variables.
    /// </summary>
    internal static class TestEnvironment
    {
        // A module initializer is the one hook every harness (Touchstone, xUnit, NUnit) runs before any suite.
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Initialize()
        {
            Environment.SetEnvironmentVariable(Xeno.Client.Hosting.XenoAppLauncher.AgentAutostartVariable, "0");
            Environment.SetEnvironmentVariable(Xeno.Client.Presentation.XenoAutoConnect.DisableVariable, "0");
        }
    }
}
