using System;
using System.Windows;

namespace KillerPDF.Services
{
    internal enum Locale { EnUS, Es, ZhTW, ZhCN, Bn, TrTR, De, Fr, JaJP }

    internal static class LocaleManager
    {
        private static Locale _current = Locale.EnUS;

        public static Locale Current => _current;

        /// <summary>
        /// Call once at startup (after ThemeManager.Initialize) to restore the saved locale.
        /// </summary>
        public static void Initialize()
        {
            var saved = App.GetSetting("Locale");
            _current = Enum.TryParse<Locale>(saved, out var l) ? l : Locale.EnUS;
            ApplyInternal(_current);
        }

        /// <summary>
        /// Switch locale, persist choice, and hot-swap the string ResourceDictionary.
        /// </summary>
        public static void Apply(Locale locale)
        {
            _current = locale;
            App.SetSetting("Locale", locale.ToString());
            ApplyInternal(locale);
        }

        // ── Internal ─────────────────────────────────────────────────────

        // Tracks the locale-override dictionary by reference (not by a fixed collection index), so
        // other dictionaries merged after the theme/strings pair (e.g. Icons.xaml in App.xaml) are
        // never disturbed by adding/removing the override here.
        private static ResourceDictionary? _localeOverrideDict;

        private static void ApplyInternal(Locale locale)
        {
            var merged = Application.Current.Resources.MergedDictionaries;

            // [0] theme. [1] en-US BASE - always present so any partial locale falls back to English for
            // keys it doesn't translate. The chosen locale's overrides (absent for English) are appended
            // and removed by reference below.
            if (merged.Count > 1)
                merged[1] = new ResourceDictionary { Source = new Uri("pack://application:,,,/Strings/en-US.xaml") };

            Uri? overrideUri = locale switch
            {
                Locale.Es   => new Uri("pack://application:,,,/Strings/es.xaml"),
                Locale.Fr   => new Uri("pack://application:,,,/Strings/fr-FR.xaml"),
                Locale.ZhTW => new Uri("pack://application:,,,/Strings/zh-TW.xaml"),
                Locale.ZhCN => new Uri("pack://application:,,,/Strings/zh-CN.xaml"),
                Locale.Bn   => new Uri("pack://application:,,,/Strings/bn.xaml"),
                Locale.TrTR => new Uri("pack://application:,,,/Strings/tr-TR.xaml"),
                Locale.De   => new Uri("pack://application:,,,/Strings/de-DE.xaml"),
                Locale.JaJP => new Uri("pack://application:,,,/Strings/ja-JP.xaml"),
                _           => null,   // English: base only
            };

            if (_localeOverrideDict is not null)
            {
                merged.Remove(_localeOverrideDict);
                _localeOverrideDict = null;
            }
            if (overrideUri is not null)
            {
                _localeOverrideDict = new ResourceDictionary { Source = overrideUri };
                merged.Add(_localeOverrideDict);
            }
        }
    }
}
