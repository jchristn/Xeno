namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Validates local inference route rules against the configured providers.
    /// Thread safe; the class is stateless.
    /// </summary>
    public static class LocalInferenceRouteValidator
    {
        /// <summary>
        /// Validate route rules and throw on the first problem. The exception's parameter name identifies the field, for example "Routes[2].Provider".
        /// </summary>
        /// <param name="routes">Route rules to validate. Null is treated as empty.</param>
        /// <param name="providers">Configured providers. Null is treated as empty.</param>
        /// <exception cref="ArgumentException">A rule has no model, names a provider that does not exist, has no provider for a local target, or repeats a model.</exception>
        public static void Validate(IEnumerable<LocalInferenceRouteRule>? routes, IEnumerable<ClientRunnerSettings>? providers)
        {
            List<LocalInferenceRouteRule> rules = (routes ?? Enumerable.Empty<LocalInferenceRouteRule>()).ToList();
            List<ClientRunnerSettings> configured = (providers ?? Enumerable.Empty<ClientRunnerSettings>()).Where(item => item != null).ToList();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rules.Count; i++)
            {
                LocalInferenceRouteRule? rule = rules[i];
                string prefix = "Routes[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                if (rule == null) throw new ArgumentException("Route rule " + (i + 1).ToString(CultureInfo.InvariantCulture) + " is empty.", prefix);

                string model = LocalModelNameNormalizer.Normalize(rule.Model);
                if (model.Length == 0) throw new ArgumentException("Route rule " + (i + 1).ToString(CultureInfo.InvariantCulture) + " needs a model name.", prefix + ".Model");
                if (!seen.Add(model)) throw new ArgumentException("Model '" + rule.Model + "' has more than one route rule. Keep one rule per model.", prefix + ".Model");

                if (rule.Target != LocalInferenceRouteTargetEnum.Local) continue;
                if (String.IsNullOrWhiteSpace(rule.Provider))
                {
                    throw new ArgumentException("Route rule for model '" + rule.Model + "' sends it to a local provider but names no provider.", prefix + ".Provider");
                }

                bool exists = configured.Any(item => String.Equals(item.Name, rule.Provider, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    throw new ArgumentException("Route rule for model '" + rule.Model + "' names provider '" + rule.Provider + "', which is not configured on this machine.", prefix + ".Provider");
                }
            }
        }
    }
}
