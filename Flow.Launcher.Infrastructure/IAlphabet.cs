namespace Flow.Launcher.Infrastructure
{
    /// <summary>
    /// Translate a language to English letters using a given rule.
    /// </summary>
    public interface IAlphabet
    {
        /// <summary>
        /// Translate a string to English letters, using a given rule.
        /// </summary>
        /// <param name="stringToTranslate">String to translate.</param>
        /// <returns></returns>
        public (string translation, TranslationMapping map) Translate(string stringToTranslate);

        /// <summary>
        /// Determine if a string should be translated to English letter with this Alphabet.
        /// </summary>
        /// <param name="stringToTranslate">String to translate.</param>
        /// <returns></returns>
        public bool ShouldTranslate(string stringToTranslate);
    }
}
