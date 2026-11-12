using System;

namespace Flow.Launcher.Core.Configuration
{
    // Switching portability relies on Squirrel.Windows (restart, shortcuts, uninstaller entry); unavailable on this platform.
    public partial class Portable
    {
        public void DisablePortableMode()
        {
            PublicApi.Instance.LogWarn(ClassName, "Portable mode is not supported on this platform");
        }

        public void EnablePortableMode()
        {
            PublicApi.Instance.LogWarn(ClassName, "Portable mode is not supported on this platform");
        }

        public void RemoveShortcuts() => throw new PlatformNotSupportedException();

        public void RemoveUninstallerEntry() => throw new PlatformNotSupportedException();

        public void CreateShortcuts() => throw new PlatformNotSupportedException();

        public void CreateUninstallerEntry() => throw new PlatformNotSupportedException();
    }
}
