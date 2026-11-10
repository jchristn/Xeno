namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Routing;

    /// <summary>
    /// One route rule as the Local endpoint page lists it.
    /// </summary>
    public sealed class RouteRuleRow
    {
        /// <summary>
        /// Model. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Target text, for example "local-ollama" or "Xeno network". Never null.
        /// </summary>
        public string Target { get; set; } = String.Empty;

        /// <summary>
        /// "yes" or "no". Never null.
        /// </summary>
        public string Fallback { get; set; } = "no";

        /// <summary>
        /// Where the model goes right now, which can differ from the rule when its provider is down. Never null.
        /// </summary>
        public string RoutesTo { get; set; } = String.Empty;

        /// <summary>
        /// The rule.
        /// </summary>
        public LocalInferenceRouteRule Source { get; set; } = new LocalInferenceRouteRule();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="rule">Rule.</param>
        /// <param name="decision">Current decision for the rule's model.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">rule or decision is null.</exception>
        public static RouteRuleRow From(LocalInferenceRouteRule rule, LocalInferenceRouteDecision decision)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentNullException.ThrowIfNull(decision);
            return new RouteRuleRow
            {
                Model = rule.Model,
                Target = rule.Target == LocalInferenceRouteTargetEnum.Network ? "Xeno network" : (rule.Provider ?? String.Empty),
                Fallback = rule.FallbackToNetwork ? "yes" : "no",
                RoutesTo = decision.RouteHeader,
                Source = rule
            };
        }
    }
}
