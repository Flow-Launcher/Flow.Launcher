using System;

namespace Flow.Launcher.Localization.Attributes
{
    /// <summary>
    /// Attribute to mark a localization key for an enum field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnumLocalizeKeyAttribute : Attribute
    {
        public static readonly EnumLocalizeKeyAttribute Default = new EnumLocalizeKeyAttribute();

        public EnumLocalizeKeyAttribute() : this(string.Empty)
        {
        }

        public EnumLocalizeKeyAttribute(string enumLocalizeKey)
        {
            EnumLocalizeKey = enumLocalizeKey;
        }

        public virtual string LocalizeKey => EnumLocalizeKey;

        protected string EnumLocalizeKey { get; set; }

        public override bool Equals(object obj) =>
            obj is EnumLocalizeKeyAttribute other && other.LocalizeKey == LocalizeKey;

        public override int GetHashCode() => LocalizeKey?.GetHashCode() ?? 0;

        public override bool IsDefaultAttribute() => Equals(Default);
    }
}
