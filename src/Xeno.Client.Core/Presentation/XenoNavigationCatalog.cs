namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Navigation shared by the desktop app sidebar, the terminal app sidebar, and the View menu in both.
    /// </summary>
    public static class XenoNavigationCatalog
    {
        /// <summary>
        /// Every page, in display order.
        /// </summary>
        public static readonly IReadOnlyList<XenoPageDefinition> Pages = new List<XenoPageDefinition>
        {
            new XenoPageDefinition { Page = XenoPageEnum.Overview, Title = "Overview", Section = "Client", ShortcutDigit = 1, Command = XenoCommandEnum.GoOverview, Description = "This machine's connection to the Xeno network, at a glance." },
            new XenoPageDefinition { Page = XenoPageEnum.Models, Title = "Models", Section = "Models", ShortcutDigit = 2, Command = XenoCommandEnum.GoModels, Description = "Models this machine serves and models the Xeno network offers it." },
            new XenoPageDefinition { Page = XenoPageEnum.Providers, Title = "Providers", Section = "Models", ShortcutDigit = 3, Command = XenoCommandEnum.GoProviders, Description = "Local inference endpoints (Ollama, vLLM, OpenAI-compatible) that serve models for the network." },
            new XenoPageDefinition { Page = XenoPageEnum.LocalEndpoint, Title = "Local endpoint", Section = "Models", ShortcutDigit = 0, Command = XenoCommandEnum.GoLocalEndpoint, Description = "The URL apps on this machine call, where each model goes, and how busy and healthy each provider is." },
            new XenoPageDefinition { Page = XenoPageEnum.Requests, Title = "Requests", Section = "Activity", ShortcutDigit = 4, Command = XenoCommandEnum.GoRequests, Description = "Requests this machine served for the Xeno network." },
            new XenoPageDefinition { Page = XenoPageEnum.Logs, Title = "Logs", Section = "Activity", ShortcutDigit = 5, Command = XenoCommandEnum.GoLogs, Description = "The client worker log." },
            new XenoPageDefinition { Page = XenoPageEnum.HostTelemetry, Title = "Host", LongTitle = "Host Telemetry", Section = "Telemetry", ShortcutDigit = 6, Command = XenoCommandEnum.GoHostTelemetry, Description = "CPU, memory, disk, and GPU usage on this machine." },
            new XenoPageDefinition { Page = XenoPageEnum.ProviderTelemetry, Title = "Provider", LongTitle = "Provider Telemetry", Section = "Telemetry", ShortcutDigit = 7, Command = XenoCommandEnum.GoProviderTelemetry, Description = "Health, loaded models, and queue depth reported by local providers." },
            new XenoPageDefinition { Page = XenoPageEnum.Server, Title = "General", Section = "Configuration", ShortcutDigit = 8, Command = XenoCommandEnum.GoServer, Description = "The Xeno server this client connects to, the account it signs in with, and its connection checks." },
            new XenoPageDefinition { Page = XenoPageEnum.Settings, Title = "Settings", Section = "Configuration", ShortcutDigit = 9, Command = XenoCommandEnum.GoSettings, Description = "Local dashboard, local inference server, history retention, and telemetry." }
        };

        /// <summary>
        /// Section names in display order.
        /// </summary>
        public static IReadOnlyList<string> Sections
        {
            get { return Pages.Select(item => item.Section).Distinct(StringComparer.Ordinal).ToList(); }
        }

        /// <summary>
        /// Find a page definition.
        /// </summary>
        /// <param name="page">Page.</param>
        /// <returns>Definition.</returns>
        public static XenoPageDefinition Get(XenoPageEnum page)
        {
            return Pages.First(item => item.Page == page);
        }

        /// <summary>
        /// Find the page a navigation command opens.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <returns>Page, or null when the command is not a navigation command.</returns>
        public static XenoPageEnum? PageFor(XenoCommandEnum command)
        {
            XenoPageDefinition? definition = Pages.FirstOrDefault(item => item.Command == command);
            return definition?.Page;
        }
    }
}
