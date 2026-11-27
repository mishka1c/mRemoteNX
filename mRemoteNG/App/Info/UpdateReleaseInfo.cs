using System;
using System.Runtime.Versioning;

namespace mRemoteNG.App.Info
{
    [SupportedOSPlatform("windows")]
    public static class UpdateReleaseInfo
    {
        // mRemoteNX: updates come only from this fork's own releases, never from upstream.
        private const string GITHUB_API_URI = "https://api.github.com/repos/" + GeneralAppInfo.ForkOwner + "/" + GeneralAppInfo.ForkRepo + "/releases/latest";

        // This fork distributes exclusively via GitHub Releases, so the update check always queries
        // the latest published release. The upstream project's Stable/Preview/Nightly text-file
        // channels (under mremoteng.org) never applied to this fork and have been removed.
        public static Uri GetLatestReleaseApiUri()
        {
            return new Uri(GITHUB_API_URI);
        }
    }
}
