using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.SmartFormat;

// Typed arguments come exclusively from the existing catalog/resolver.
public sealed class StatSmartDescription
{
    public static bool RequiresSmart(string text, StatDescriptionSettings settings)
    {
        foreach (var insertion in settings.insertions)
            if (insertion != null && text.Contains("{" + insertion.token + ":")) return true;
        return false;
    }
    private readonly Dictionary<string, object> arguments = new();
    private readonly List<StatValueDefinition> definitions = new();
    private readonly SmartFormatter formatter = Smart.CreateDefaultSmartFormat();
    private StatDescriptionSettings settings;
    private string sentence;
    public string Warning { get; private set; }
    public void Compile(string text, StatDescriptionSettings description, StatValueCatalog catalog)
    {
        settings = description; sentence = text; definitions.Clear();
        foreach (var insertion in settings.insertions)
        {
            StatValueDefinition d = null; if (insertion?.binding != null) catalog?.TryFind(insertion.binding, out d);
            definitions.Add(d);
        }
    }
    public string Render(IStatValueResolver resolver, IFormatProvider culture)
    {
        Warning = null; arguments.Clear(); string text = sentence; bool baseRaw = false;
        for (int i = 0; i < definitions.Count; i++)
        {
            var insertion = settings.insertions[i]; if (insertion?.binding == null || string.IsNullOrEmpty(insertion.token)) continue;
            var d = definitions[i];
            var result = d != null ? d.Read(resolver) : new StatValueResult(null, StatValueState.Missing);
            bool bare = text.Contains("{" + insertion.token + "}");
            bool raw = text.Contains("{" + insertion.token + ":");
            if (raw && result.State == StatValueState.Missing) { Warning = "Missing stat: " + insertion.token; return "— [missing: " + insertion.token + "]"; }
            baseRaw |= raw && result.State == StatValueState.BaseConfiguration;
            string display;
            if (d == null) { display = "— [missing]"; Warning = "Missing stat: " + insertion.token; }
            else { display = StatValueFormatter.FormatResult(d, result, bare ? insertion.binding.valueFormat : "", out var warning, culture); if (bare) Warning ??= warning; }
            arguments[insertion.token] = result.State == StatValueState.Missing ? display : result.Value;
            if (settings.boldValues) display = "<b>" + display + "</b>";
            if (settings.colorValues) display = "<color=#" + ColorUtility.ToHtmlStringRGBA(settings.valueColor) + ">" + display + "</color>";
            string alias = "__display_" + insertion.token;
            arguments[alias] = display;
            // Only bare display tokens use Value Format. Plural/explicit format clauses
            // keep the original typed value and Unity's locale-dependent formatting.
            text = text.Replace("{" + insertion.token + "}", "{" + alias + "}");
        }
        try { return formatter.Format(culture, text, arguments) + (baseRaw ? " [base]" : ""); }
        catch (Exception e) when (e is FormatException || e.GetType().Namespace?.Contains("SmartFormat") == true)
        { Warning = e.Message; return "— [format]"; }
    }
}
