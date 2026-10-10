using System.Threading.Tasks;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core
{
    public partial class Updater
    {
        public string GitHubRepository { get; init; }

        private static readonly string ClassName = nameof(Updater);

        private readonly IPublicAPI _api;

        public Updater(IPublicAPI publicAPI, string gitHubRepository)
        {
            _api = publicAPI;
            GitHubRepository = gitHubRepository;
        }

        public partial Task UpdateAppAsync(bool silentUpdate = true);
    }
}
