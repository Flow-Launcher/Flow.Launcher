using System.Collections.Generic;

namespace Flow.Launcher.Core.Resource
{
    internal static class AvailableLanguages
    {
        public static Language English = new Language("en", "English");
        public static Language Chinese = new Language("zh-cn", "中文");
        public static Language Chinese_TW = new Language("zh-tw", "中文（繁体）");
        public static Language Ukrainian = new Language("uk-UA", "Українська");
        public static Language Russian = new Language("ru", "Русский");
        public static Language French = new Language("fr", "Français");
        public static Language Japanese = new Language("ja", "日本語");
        public static Language Dutch = new Language("nl", "Dutch");
        public static Language Polish = new Language("pl", "Polski");
        public static Language Danish = new Language("da", "Dansk");
        public static Language German = new Language("de", "Deutsch");
        public static Language Korean = new Language("ko", "한국어");
        public static Language Serbian = new Language("sr", "Srpski");
        public static Language Serbian_Cyrillic = new Language("sr-Cyrl-RS", "Српски");
        public static Language Portuguese_Portugal = new Language("pt-pt", "Português");
        public static Language Portuguese_Brazil = new Language("pt-br", "Português (Brasil)");
        public static Language Spanish = new Language("es", "Spanish");
        public static Language Spanish_LatinAmerica = new Language("es-419", "Spanish (Latin America)");
        public static Language Italian = new Language("it", "Italiano");
        public static Language Norwegian_Bokmal = new Language("nb-NO", "Norsk Bokmål");
        public static Language Slovak = new Language("sk", "Slovenčina");
        public static Language Turkish = new Language("tr", "Türkçe");
        public static Language Czech = new Language("cs", "čeština");
        public static Language Arabic = new Language("ar", "اللغة العربية");
        public static Language Vietnamese = new Language("vi-vn", "Tiếng Việt");
        public static Language Hebrew = new Language("he", "עברית");

        public static List<Language> GetAvailableLanguages()
        {
            List<Language> languages = new List<Language>
            {
                English,
                Chinese,
                Chinese_TW,
                Ukrainian,
                Russian,
                French,
                Japanese,
                Dutch,
                Polish,
                Danish,
                German,
                Korean,
                Serbian,
                Serbian_Cyrillic,
                Portuguese_Portugal,
                Portuguese_Brazil,
                Spanish,
                Spanish_LatinAmerica,
                Italian,
                Norwegian_Bokmal,
                Slovak,
                Turkish,
                Czech,
                Arabic,
                Vietnamese,
                Hebrew
            };
            return languages;
        }

        public static string GetSystemTranslation(string languageCode)
        {
            return languageCode switch
            {
                "en" => "System",
                "zh-cn" => "系统",
                "zh-tw" => "系統",
                "uk-UA" => "Система",
                "ru" => "Система",
                "fr" => "Système",
                "ja" => "システム",
                "nl" => "Systeem",
                "pl" => "System",
                "da" => "System",
                "de" => "System",
                "ko" => "시스템",
                "sr" => "Sistem",
                "sr-Cyrl-RS" => "Систем",
                "pt-pt" => "Sistema",
                "pt-br" => "Sistema",
                "es" => "Sistema",
                "es-419" => "Sistema",
                "it" => "Sistema",
                "nb-NO" => "System",
                "sk" => "Systém",
                "tr" => "Sistem",
                "cs" => "Systém",
                "ar" => "النظام",
                "vi-vn" => "Hệ thống",
                "he" => "מערכת",
                _ => "System",
            };
        }
    }
}
