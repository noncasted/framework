using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Internal
{
    /// <summary>
    ///     Runs the project <see cref="IPlayerBuildStep" />s around a player build: <see cref="IPlayerBuildStep.BeforeBuild" />
    ///     in the order of the full type names, then the build, then <see cref="IPlayerBuildStep.AfterBuild" /> in reverse
    ///     order. Not tied to a platform: the build entry point of a platform (<c>Internal.WebGlBuild</c>) wraps its whole
    ///     build in <see cref="Run" /> and turns a failure into the batchmode exit code.
    /// </summary>
    public static class PlayerBuildSteps
    {
        /// <returns>The report of <paramref name="build" />.</returns>
        /// <exception cref="AggregateException">More than one of the build and the steps threw.</exception>
        /// <remarks>
        ///     An exception of the build or of a step is rethrown only after every started step got its
        ///     <see cref="IPlayerBuildStep.AfterBuild" />.
        /// </remarks>
        public static BuildReport Run(BuildTarget target, Func<BuildReport> build)
        {
            var steps = Find();
            var started = new List<IPlayerBuildStep>(steps.Count);
            var errors = new List<Exception>();
            BuildReport report = null;

            if (steps.Count > 0)
                Debug.Log($"[PlayerBuildSteps] {target}: {string.Join(", ", steps.Select(step => step.GetType().FullName))}");

            try
            {
                foreach (var step in steps)
                {
                    // Added before the call: a step that throws halfway still cleans up what it left.
                    started.Add(step);
                    step.BeforeBuild(target);
                }

                report = build();
            }
            catch (Exception exception)
            {
                // Rethrown below, after the cleanup.
                errors.Add(exception);
            }

            var succeeded = report != null && report.summary.result == BuildResult.Succeeded;

            for (var i = started.Count - 1; i >= 0; i--)
            {
                try
                {
                    started[i].AfterBuild(target, succeeded);
                }
                catch (Exception exception)
                {
                    // One failed cleanup must not skip the others; rethrown below.
                    errors.Add(exception);
                }
            }

            if (errors.Count == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();

            if (errors.Count > 1)
                throw new AggregateException($"[PlayerBuildSteps] {target} build: {errors.Count} errors", errors);

            return report;
        }

        private static IReadOnlyList<IPlayerBuildStep> Find()
        {
            return TypeCache.GetTypesDerivedFrom<IPlayerBuildStep>()
                            .Where(type => type.IsAbstract == false)
                            .OrderBy(type => type.FullName, StringComparer.Ordinal)
                            .Select(type => (IPlayerBuildStep)Activator.CreateInstance(type))
                            .ToArray();
        }
    }
}
