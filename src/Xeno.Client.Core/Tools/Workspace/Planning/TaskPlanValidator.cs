namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Validates a proposed task plan before it is applied: unique non-empty ids and titles, dependencies that
    /// reference known tasks, no self-dependency, and no dependency cycles. Stateless and thread safe.
    /// </summary>
    public static class TaskPlanValidator
    {
        #region Public-Methods

        /// <summary>
        /// Validate proposed tasks.
        /// </summary>
        /// <param name="tasks">Proposed tasks; null or empty is a valid (empty) plan.</param>
        /// <returns>Result; <see cref="TaskPlanValidationResult.IsValid"/> is true when no problems were found.</returns>
        public static TaskPlanValidationResult Validate(IReadOnlyList<AgentTask?>? tasks)
        {
            TaskPlanValidationResult result = new TaskPlanValidationResult();
            if (tasks == null || tasks.Count == 0) return result;

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> duplicates = new HashSet<string>(StringComparer.Ordinal);

            foreach (AgentTask? task in tasks)
            {
                if (task == null)
                {
                    result.Problems.Add("A task entry was null.");
                    continue;
                }

                if (String.IsNullOrWhiteSpace(task.Id))
                {
                    result.Problems.Add("A task has an empty id.");
                }
                else if (!ids.Add(task.Id) && duplicates.Add(task.Id))
                {
                    result.Problems.Add("Duplicate task id '" + task.Id + "'.");
                }

                if (String.IsNullOrWhiteSpace(task.Title))
                {
                    result.Problems.Add("Task '" + (String.IsNullOrWhiteSpace(task.Id) ? "(no id)" : task.Id) + "' has an empty title.");
                }
            }

            foreach (AgentTask? task in tasks)
            {
                if (task == null || String.IsNullOrWhiteSpace(task.Id)) continue;

                foreach (string dependency in task.DependsOn)
                {
                    if (String.Equals(dependency, task.Id, StringComparison.Ordinal))
                    {
                        result.Problems.Add("Task '" + task.Id + "' depends on itself.");
                    }
                    else if (!ids.Contains(dependency))
                    {
                        result.Problems.Add("Task '" + task.Id + "' depends on unknown task '" + dependency + "'.");
                    }
                }
            }

            DetectCycles(tasks, ids, result);
            return result;
        }

        #endregion

        #region Private-Methods

        private static void DetectCycles(IReadOnlyList<AgentTask?> tasks, HashSet<string> ids, TaskPlanValidationResult result)
        {
            Dictionary<string, List<string>> edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (AgentTask? task in tasks)
            {
                if (task == null || String.IsNullOrWhiteSpace(task.Id)) continue;
                if (!edges.ContainsKey(task.Id)) edges[task.Id] = new List<string>();

                foreach (string dependency in task.DependsOn)
                {
                    if (ids.Contains(dependency) && !String.Equals(dependency, task.Id, StringComparison.Ordinal))
                    {
                        edges[task.Id].Add(dependency);
                    }
                }
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> onStack = new HashSet<string>(StringComparer.Ordinal);
            foreach (string node in edges.Keys)
            {
                if (!visited.Contains(node) && HasCycleFrom(node, edges, visited, onStack))
                {
                    result.Problems.Add("The plan contains a dependency cycle.");
                    return;
                }
            }
        }

        private static bool HasCycleFrom(string node, Dictionary<string, List<string>> edges, HashSet<string> visited, HashSet<string> onStack)
        {
            visited.Add(node);
            onStack.Add(node);

            if (edges.TryGetValue(node, out List<string>? dependencies))
            {
                foreach (string dependency in dependencies)
                {
                    if (!visited.Contains(dependency))
                    {
                        if (HasCycleFrom(dependency, edges, visited, onStack)) return true;
                    }
                    else if (onStack.Contains(dependency))
                    {
                        return true;
                    }
                }
            }

            onStack.Remove(node);
            return false;
        }

        #endregion
    }
}
