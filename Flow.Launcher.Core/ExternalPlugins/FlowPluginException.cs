using Flow.Launcher.Plugin;
using System;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public class FlowPluginException : Exception
    {
        public PluginMetadata Metadata { get; set; }
        
        public FlowPluginException(PluginMetadata metadata, Exception e) : base(e.Message, e)
        {
            Metadata = metadata;
        }

        public override string ToString()
        {
            return $@"{Metadata.Name} Exception: 
Websites: {Metadata.Website}
Author: {Metadata.Author}
Version: {Metadata.Version}
{base.ToString()}";
        }
    }
}