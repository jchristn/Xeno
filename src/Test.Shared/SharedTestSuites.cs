namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Core;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;

    /// <summary>
    /// Shared Touchstone test suites for Xeno.
    /// </summary>
    public static class SharedTestSuites
    {
        /// <summary>
        /// All shared suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    SettingsSuite(),
                    DatabaseProviderSuite(),
                    RoutingSuite(),
                    TenantIsolationSuite()
                };
            }
        }

        /// <summary>
        /// Settings test suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "DefaultSettingsContainRequiredSections",
                        displayName: "Default settings contain required sections",
                        executeAsync: delegate
                        {
                            XenoSettings settings = new XenoSettings();
                            if (settings.Rest == null) throw new InvalidOperationException("Rest settings missing.");
                            if (settings.Cors == null) throw new InvalidOperationException("CORS settings missing.");
                            if (settings.WebSocket == null) throw new InvalidOperationException("WebSocket settings missing.");
                            if (settings.Database == null) throw new InvalidOperationException("Database settings missing.");
                            if (settings.Logging == null) throw new InvalidOperationException("Logging settings missing.");
                            return Task.CompletedTask;
                        })
                });
        }

        /// <summary>
        /// Database provider suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DatabaseProviderSuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            foreach (DatabaseTypeEnum provider in Enum.GetValues<DatabaseTypeEnum>())
            {
                cases.Add(new TestCaseDescriptor(
                    suiteId: "Database",
                    caseId: "Initialize" + provider,
                    displayName: "Initialize " + provider,
                    executeAsync: async delegate
                    {
                        DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Type = provider }).ConfigureAwait(false);
                        if (driver.DatabaseType != provider) throw new InvalidOperationException("Provider mismatch.");
                        if (driver.Platform.Tenants.Count < 1) throw new InvalidOperationException("Seed tenant missing.");
                    }));
            }

            return new TestSuiteDescriptor("Database", "Database Providers", cases);
        }

        /// <summary>
        /// Routing suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RoutingSuite()
        {
            return new TestSuiteDescriptor(
                "Routing",
                "Routing",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "Routing",
                        "CompletionRoutes",
                        "Completion requests route to a client endpoint",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (String.IsNullOrWhiteSpace(decision.SelectedEndpointId)) throw new InvalidOperationException("Endpoint not selected.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "EmbeddingRoutes",
                        "Embedding requests route to an embedding-capable model",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "text-embedding-3-small", true).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.EffectiveModel != "text-embedding-3-small") throw new InvalidOperationException("Effective model mismatch.");
                        })
                });
        }

        /// <summary>
        /// Tenant isolation suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TenantIsolationSuite()
        {
            return new TestSuiteDescriptor(
                "TenantIsolation",
                "Tenant Isolation",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "TenantIsolation",
                        "CrossTenantRouteDenied",
                        "Cross-tenant routing does not leak endpoints",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = "ten_other", IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (decision.Success) throw new InvalidOperationException("Cross-tenant route unexpectedly succeeded.");
                        }),
                    new TestCaseDescriptor(
                        "TenantIsolation",
                        "TenantScopedEnumeration",
                        "Tenant scoped model enumeration is isolated",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            int defaultCount = platform.ModelDefinitions.Count(item => item.TenantId == Constants.DefaultTenantId);
                            int otherCount = platform.ModelDefinitions.Count(item => item.TenantId == "ten_other");
                            if (defaultCount < 1) throw new InvalidOperationException("Default tenant models missing.");
                            if (otherCount != 0) throw new InvalidOperationException("Unexpected cross-tenant models.");
                        })
                });
        }
    }
}
