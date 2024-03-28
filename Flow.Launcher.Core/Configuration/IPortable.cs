
namespace Flow.Launcher.Core.Configuration
{
    public interface IPortable
    {
        void EnablePortableMode();
        void DisablePortableMode();
        bool CanUpdatePortability();
    }
}
