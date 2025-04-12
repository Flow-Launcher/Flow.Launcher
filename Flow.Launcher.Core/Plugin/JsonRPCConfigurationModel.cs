using System;
using System.Collections.Generic;

namespace Flow.Launcher.Core.Plugin
{
    public class JsonRpcConfigurationModel
    {
        public List<SettingField> Body { get; set; }
        public void Deconstruct(out List<SettingField> Body)
        {
            Body = this.Body;
        }
    }

    public class SettingField
    {
        public string Type { get; set; }
        public FieldAttributes Attributes { get; set; }
        public void Deconstruct(out string Type, out FieldAttributes attributes)
        {
            Type = this.Type;
            attributes = this.Attributes;
        }
    }
    public class FieldAttributes
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string urlLabel { get; set; }
        public Uri url { get; set; }
        public bool Validation { get; set; }
        public List<string> Options { get; set; }
        public string DefaultValue { get; set; }
        public char passwordChar { get; set; }
        public void Deconstruct(out string Name, out string Label, out string Description, out bool Validation, out List<string> Options, out string DefaultValue)
        {
            Name = this.Name;
            Label = this.Label;
            Description = this.Description;
            Validation = this.Validation;
            Options = this.Options;
            DefaultValue = this.DefaultValue;
        }
    }
}
