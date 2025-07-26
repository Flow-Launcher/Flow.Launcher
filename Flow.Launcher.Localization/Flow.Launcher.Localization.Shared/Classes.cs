using Microsoft.CodeAnalysis;

namespace Flow.Launcher.Localization.Shared
{
    public class PluginClassInfo
    {
        public Location Location { get; }
        public string ClassName { get; }
        public string PropertyName { get; }
        public bool IsStatic { get; }
        public bool IsPrivate { get; }
        public bool IsProtected { get; }
        public Location CodeFixLocation { get; }

        public string ContextAccessor => $"{ClassName}.{PropertyName}";
        public bool IsValid => PropertyName != null && IsStatic && (!IsPrivate) && (!IsProtected);

        public PluginClassInfo(Location location, string className, string propertyName, bool isStatic, bool isPrivate, bool isProtected, Location codeFixLocation)
        {
            Location = location;
            ClassName = className;
            PropertyName = propertyName;
            IsStatic = isStatic;
            IsPrivate = isPrivate;
            IsProtected = isProtected;
            CodeFixLocation = codeFixLocation;
        }
    }
}
