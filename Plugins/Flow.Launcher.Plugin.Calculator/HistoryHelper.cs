using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Plugin.Calculator
{
    /// <summary>
    /// Helper class providing utility methods for formatting relative time strings.
    /// </summary>
    internal static class HistoryHelper
    {
        /// <summary>
        /// Formats a DateTime value into a localized relative time string (e.g. "just now", "5 minutes ago").
        /// </summary>
        /// <param name="context">The plugin init context used to retrieve localized strings.</param>
        /// <param name="calculatedAt">The time when the calculation was recorded.</param>
        /// <returns>A localized relative time delta string.</returns>
        public static string GetTimeDeltaString(PluginInitContext context, DateTime calculatedAt)
        {
            var now = DateTime.Now;
            var timeSpan = now - calculatedAt;

            if (timeSpan.TotalSeconds < 0)
            {
                timeSpan = TimeSpan.Zero;
            }

            if (timeSpan.TotalSeconds < 60)
            {
                return context == null ? "just now" : Localize.flowlauncher_plugin_calculator_time_just_now();
            }
            if (timeSpan.TotalMinutes < 60)
            {
                var minutes = (int)timeSpan.TotalMinutes;
                if (minutes == 1)
                {
                    return context == null ? "1 minute ago" : Localize.flowlauncher_plugin_calculator_time_minute_ago();
                }
                return context == null ? $"{minutes} minutes ago" : Localize.flowlauncher_plugin_calculator_time_minutes_ago(minutes);
            }
            if (timeSpan.TotalHours < 24)
            {
                var hours = (int)timeSpan.TotalHours;
                if (hours == 1)
                {
                    return context == null ? "1 hour ago" : Localize.flowlauncher_plugin_calculator_time_hour_ago();
                }
                return context == null ? $"{hours} hours ago" : Localize.flowlauncher_plugin_calculator_time_hours_ago(hours);
            }
            if (timeSpan.TotalDays < 30)
            {
                var days = (int)timeSpan.TotalDays;
                if (days == 1)
                {
                    return context == null ? "1 day ago" : Localize.flowlauncher_plugin_calculator_time_day_ago();
                }
                return context == null ? $"{days} days ago" : Localize.flowlauncher_plugin_calculator_time_days_ago(days);
            }
            if (timeSpan.TotalDays < 365)
            {
                var months = (int)(timeSpan.TotalDays / 30);
                if (months == 1)
                {
                    return context == null ? "1 month ago" : Localize.flowlauncher_plugin_calculator_time_month_ago();
                }
                return context == null ? $"{months} months ago" : Localize.flowlauncher_plugin_calculator_time_months_ago(months);
            }
            var years = (int)(timeSpan.TotalDays / 365);
            if (years == 1)
            {
                return context == null ? "1 year ago" : Localize.flowlauncher_plugin_calculator_time_year_ago();
            }
            return context == null ? $"{years} years ago" : Localize.flowlauncher_plugin_calculator_time_years_ago(years);
        }
    }
}
