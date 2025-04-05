namespace Flow.Launcher.Plugin
{
    /// <summary>
    /// Plugin instance and plugin metadata
    /// </summary>
    public class PluginPair
    {
        /// <summary>
        /// Plugin instance
        /// </summary>
        public IAsyncPlugin Plugin { get; internal set; }

        /// <summary>
        /// Plugin metadata
        /// </summary>
        public PluginMetadata Metadata { get; internal set; }

        /// <summary>
        /// Convert to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Metadata.Name;
        }

        /// <summary>
        /// Compare by plugin metadata ID
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is PluginPair r)
            {
                return string.Equals(r.Metadata.ID, Metadata.ID);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hashcode = Metadata.ID?.GetHashCode() ?? 0;
            return hashcode;
        }
    }
}
