using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[Serializable]
public sealed class StatDescriptionSettings
{
    [TextArea(3, 8)] public string text = "";
    public LocalizedString localizedText = new();
    public bool boldValues;
    public Color valueColor = Color.white;
    public bool colorValues;
    public List<StatDescriptionInsertion> insertions = new();
}

[Serializable]
public sealed class StatDescriptionInsertion
{
    public string token;
    public StatDisplayCell binding = new() { useBinding = true };
}

// Compiled against the full catalog, independently of which cards the designer displays.
public sealed class StatDescriptionTemplate
{
    private readonly List<Part> parts = new();
    private readonly struct Part
    {
        public readonly string Text;
        public readonly StatValueDefinition Definition;
        public readonly StatDisplayCell Binding;
        public Part(string text, StatValueDefinition definition = null, StatDisplayCell binding = null)
        { Text = text; Definition = definition; Binding = binding; }
    }
    public void Compile(string template, StatDescriptionSettings settings, StatValueCatalog catalog)
    {
        parts.Clear(); template ??= "";
        int start = 0;
        while (start < template.Length)
        {
            int open = template.IndexOf('{', start);
            if (open < 0) { parts.Add(new Part(template.Substring(start))); break; }
            if (open > start) parts.Add(new Part(template.Substring(start, open - start)));
            if (open + 1 < template.Length && template[open + 1] == '{')
            { parts.Add(new Part("{")); start = open + 2; continue; }
            int close = template.IndexOf('}', open + 1);
            if (close < 0) { parts.Add(new Part(template.Substring(open))); break; }
            string token = template.Substring(open + 1, close - open - 1);
            StatDescriptionInsertion selected = null;
            foreach (var insertion in settings.insertions)
                if (insertion != null && insertion.token == token) { selected = insertion; break; }
            if (selected?.binding == null) parts.Add(new Part("[missing: " + token + "]"));
            else
            {
                StatValueDefinition definition = null;
                catalog?.TryFind(selected.binding, out definition);
                parts.Add(new Part(null, definition, selected.binding));
            }
            start = close + 1;
        }
    }
    public string Render(IStatValueResolver resolver, StatDescriptionSettings settings, IFormatProvider culture = null)
    {
        var result = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (part.Binding == null) { result.Append(part.Text); continue; }
            string value = StatValueFormatter.Format(part.Definition, resolver, part.Binding.valueFormat, out _, culture);
            if (settings.boldValues) result.Append("<b>");
            if (settings.colorValues) result.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(settings.valueColor)).Append('>');
            result.Append(value);
            if (settings.colorValues) result.Append("</color>");
            if (settings.boldValues) result.Append("</b>");
        }
        return result.ToString();
    }
}
