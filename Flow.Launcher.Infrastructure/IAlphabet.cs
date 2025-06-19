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
        /// <summary>
/// Translates the input string into English letters according to the implementing alphabet's rules.
/// </summary>
/// <param name="stringToTranslate">The string to be translated.</param>
/// <returns>A tuple containing the translated string and a <see cref="TranslationMapping"/> representing the mapping details.</returns>
        public (string translation, TranslationMapping map) Translate(string stringToTranslate);

        /// <summary>
        /// Determine if a string can be translated to English letter with this Alphabet.
        /// </summary>
        /// <param name="stringToTranslate">String to translate.</param>
        /// <summary>
/// Determines whether the specified string is eligible for translation to English letters using this alphabet's rules.
/// </summary>
/// <param name="stringToTranslate">The input string to evaluate for translation eligibility.</param>
/// <returns>True if the string should be translated; otherwise, false.</returns>
        public bool ShouldTranslate(string stringToTranslate);
    }
}
