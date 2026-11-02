using System;

// The single formatting/status path for cards and inline descriptions, for every value kind.
public static class StatValueFormatter
{
    public static System.Globalization.CultureInfo Culture
    {
        get
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.HasSettings)
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleAsync;
                if (locale.IsDone && locale.Result != null && locale.Result.Identifier.CultureInfo != null) return locale.Result.Identifier.CultureInfo;
            }
            return System.Globalization.CultureInfo.CurrentCulture;
        }
    }
    public static string Format(StatValueDefinition definition, IStatValueResolver resolver, string format, out string warning, IFormatProvider culture = null)
    {
        warning = null;
        if (definition == null) { warning = "Missing stat binding"; return "— [missing]"; }
        return FormatResult(definition, definition.Read(resolver), format, out warning, culture);
    }
    public static string FormatResult(StatValueDefinition definition, StatValueResult result, string format, out string warning, IFormatProvider culture = null)
    {
        warning = null;
        try
        {
            if (result.State == StatValueState.Missing) { warning = "Stat source unavailable: " + definition.SourceLabel; return "— [missing]"; }
            string value = definition.FormatValue(result.Value, format, culture);
            return result.State == StatValueState.BaseConfiguration ? value + " [base]" : value;
        }
        catch (FormatException) { warning = "Invalid stat format: " + format; return "— [format]"; }
    }
}
