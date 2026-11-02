using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class VerifySmartDescriptions
{
    sealed class Resolver : IStatValueResolver
    {
        public object Value;
        public bool IsRuntime => true;
        public bool TryRead(StatValueDefinition d, out StatValueResult result) { result = new StatValueResult(Value, StatValueState.Runtime); return true; }
    }
    public static object Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        var meta = ScriptableObject.CreateInstance<WeaponMetaConfig>();
        try
        {
            var catalog = StatCatalogBuilder.Build(meta); var definition = catalog.Values.First(d => d.Property == "Damage");
            var settings = new StatDescriptionSettings(); settings.insertions.Add(new StatDescriptionInsertion { token = "stat1", binding = new StatDisplayCell
                { useBinding = true, sourcePath = definition.SourcePath, statId = definition.Id, sourceAsset = definition.SourceAsset } });
            var resolver = new Resolver(); var renderer = new StatSmartDescription();
            string Render(string text, string locale) { renderer.Compile(text, settings, catalog); return renderer.Render(resolver, CultureInfo.GetCultureInfo(locale)); }
            int[] counts = {1,3,5,21,22,25}; string[] forms = {"заряд","заряда","зарядов","заряд","заряда","зарядов"};
            for (int i = 0; i < counts.Length; i++)
            {
                resolver.Value = counts[i];
                string expected = "Имеет " + counts[i] + " " + forms[i] + ".";
                Check(Render("Имеет {stat1} {stat1:plural:заряд|заряда|зарядов}.", "ru") == expected, expected);
            }
            resolver.Value = 1; Check(Render("Has {stat1} {stat1:plural:charge|charges}.", "en") == "Has 1 charge.", "English singular");
            resolver.Value = 3; Check(Render("Has {stat1} {stat1:plural:charge|charges}.", "en") == "Has 3 charges.", "English plural");
            resolver.Value = 1.5f; Check(Render("{stat1:N1} секунды", "ru") == "1,5 секунды", "RU decimal");
            Check(Render("{stat1:N1} seconds", "en") == "1.5 seconds", "EN decimal");
            settings.insertions[0].binding.valueFormat = "{0:0.0}%";
            resolver.Value = 22; Check(Render("{stat1} {stat1:plural:заряд|заряда|зарядов}", "ru") == "22,0% заряда", "Formatted display leaves plural argument numeric");
            Check(Render("{stat1:0.00}", "en") == "22.00", "Explicit Smart format overrides Value Format once");
            resolver.Value = "Plasma"; settings.insertions[0].binding.valueFormat = ""; definition.Format = "";
            Check(Render("Type: {stat1}", "en") == "Type: Plasma", "String values remain supported");
            resolver.Value = EnemyDamageType.Kinetick;
            Check(Render("Type: {stat1}", "en") == "Type: Kinetick", "Enum values remain supported");
            Check(Render("{stat1:plural:one|many}", "en").Contains("[format]"), "Invalid plural argument diagnosed");
            resolver.Value = 3;
            Check(Render("{stat1:plural:charge|charges}: {stat1}", "en") == "charges: 3", "Translation reorder");
            Check(StatSmartDescription.RequiresSmart("{stat1:N1}", settings), "Non-Smart numeric formatter warning");
            Check(!StatSmartDescription.RequiresSmart("{stat1}", settings), "Legacy bare tokens remain ordinary");
            var locale = UnityEngine.Localization.Locale.CreateLocale("ru");
            try
            {
                resolver.Value = 22; renderer.Compile("{stat1} {stat1:plural:заряд|заряда|зарядов}", settings, catalog);
                Check(renderer.Render(resolver, locale) == "22 заряда", "Unity Locale provider uses Russian plural rule");
            }
            finally { UnityEngine.Object.DestroyImmediate(locale); }
            return new { passed = true, checks, russian = "1,3,5,21,22,25", cultures = "ru/en" };
        }
        finally { UnityEngine.Object.DestroyImmediate(meta); }
    }
}
