using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Core.ExternalPlugins
{
    /// <summary>
    /// Describes a store of community-made plugins.
    /// The provided URLs should point to a json file, whose content
    /// is deserializable as a <see cref="UserPlugin"/> array.
    /// </summary>
    /// <param name="primaryUrl">Primary URL to the manifest json file.</param>
    /// <param name="secondaryUrls">Secondary URLs to access the <paramref name="primaryUrl"/>, for example CDN links</param>
    public record CommunityPluginStore(string primaryUrl, params string[] secondaryUrls)
    {
        private readonly List<CommunityPluginSource> pluginSources =
            secondaryUrls
                .Append(primaryUrl)
                .Select(url => new CommunityPluginSource(url))
                .ToList();

        public async Task<List<UserPlugin>> FetchAsync(CancellationToken token, bool onlyFromPrimaryUrl = false)
        {
            // we create a new cancellation token source linked to the given token.
            // Once any of the http requests completes successfully, we call cancel
            // to stop the rest of the running http requests.
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            var tasks = onlyFromPrimaryUrl
                ? new() { pluginSources.Last().FetchAsync(cts.Token) }
                : pluginSources.Select(pluginSource => pluginSource.FetchAsync(cts.Token)).ToList();

            var pluginResults = new List<UserPlugin>();

            // keep going until all tasks have completed
            while (tasks.Any())
            {
                var completedTask = await Task.WhenAny(tasks);
                if (completedTask.IsCompletedSuccessfully)
                {
                    // one of the requests completed successfully; keep its results
                    // and cancel the remaining http requests.
                    pluginResults = await completedTask;
                    cts.Cancel();
                }
                tasks.Remove(completedTask);
            }

            // all tasks have finished
            return pluginResults;
        }
    }
}
