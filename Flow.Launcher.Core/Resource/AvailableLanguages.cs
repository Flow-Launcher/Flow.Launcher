﻿using System.Collections.Generic;

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
                Vietnamese
            };
            return languages;
        }
    }
}
