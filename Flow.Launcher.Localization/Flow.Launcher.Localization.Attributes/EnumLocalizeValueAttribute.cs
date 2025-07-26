using System;

namespace Flow.Launcher.Localization.Attributes
{
    /// <summary>
    /// Attribute to mark a localization value for an enum field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnumLocalizeValueAttribute : Attribute
    {
        public static readonly EnumLocalizeValueAttribute Default = new EnumLocalizeValueAttribute();

        public EnumLocalizeValueAttribute() : this(string.Empty)
        {
        }

        public EnumLocalizeValueAttribute(string enumLocalizeValue)
        {
            EnumLocalizeValue = enumLocalizeValue;
        }

        public virtual string LocalizeValue => EnumLocalizeValue;

        protected string EnumLocalizeValue { get; set; }

        public override bool Equals(object obj) =>
            obj is EnumLocalizeValueAttribute other && other.LocalizeValue == LocalizeValue;

        public override int GetHashCode() => LocalizeValue?.GetHashCode() ?? 0;

        public override bool IsDefaultAttribute() => Equals(Default);
    }
}
