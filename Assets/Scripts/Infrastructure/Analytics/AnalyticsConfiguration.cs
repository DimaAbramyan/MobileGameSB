using System;
using UnityEngine;

namespace Game.Infrastructure.Analytics
{
    public enum AnalyticsReleaseEnvironment { Development, Production }

    [Serializable]
    public sealed class AnalyticsConfiguration
    {
        // Release QA builds are safe by default too. Production requires an explicit release choice.
        [Tooltip("Editor and Development Builds always use development. Choose Production explicitly only for the release rollout; release QA builds can stay in Development.")]
        public AnalyticsReleaseEnvironment releaseEnvironment = AnalyticsReleaseEnvironment.Development;

        public string ResolveEnvironment(bool isEditor, bool isDevelopmentBuild)
            => !isEditor && !isDevelopmentBuild && releaseEnvironment == AnalyticsReleaseEnvironment.Production
                ? "production" : "development";
    }
}
