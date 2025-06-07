namespace Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks
{
    public class AccessLink
    {
        public string Path { get; set; }

        public ResultType Type { get; set; } = ResultType.Folder;

        public string Name { get; set; }
    }
}
