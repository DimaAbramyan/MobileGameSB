using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VerifyStatDescriptions
{
    sealed class Resolver : IStatValueResolver
    {
        public float Damage = 20;
        public bool IsRuntime => true;
        public bool TryRead(StatValueDefinition d, out StatValueResult value)
        { value = new StatValueResult(Damage, StatValueState.Runtime); return d.ProviderType == typeof(WeaponMetaBasicStats) && d.Property == "Damage"; }
    }
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static StatDisplayCell Cell(StatValueDefinition d) => new() { useBinding = true, sourcePath = d.SourcePath, statId = d.Id, sourceAsset = d.SourceAsset };
    static void Ref(UnityEngine.Object target, string name, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    public static object Run()
    {
        checks = 0; int values = 0;
        var meta = ScriptableObject.CreateInstance<WeaponMetaConfig>();
        GameObject panel = null, textObject = null;
        try
        {
            var catalog = StatCatalogBuilder.Build(meta);
            var damage = catalog.Values.Single(d => d.Property == "Damage");
            meta.Description.text = "Damage: .";
            StatDescriptionEditor.Insert(meta, "description", 8, damage);
            Check(meta.Description.text == "Damage: {stat1}.", "Inspector inserts a stable token at cursor");
            Check(meta.StatDisplay.baseStats.rows.Count == 0, "Description selection independent of cards");
            var template = new StatDescriptionTemplate();
            var resolver = new Resolver();
            template.Compile(meta.Description.text, meta.Description, catalog);
            Check(template.Render(resolver, meta.Description) == "Damage: 20.", "Same runtime resolver without quotes");
            resolver.Damage = 25;
            Check(template.Render(resolver, meta.Description) == "Damage: 25.", "Cached template updates after modifier");
            meta.Description.insertions[0].binding.valueFormat = "{0:0.0}%";
            Check(template.Render(resolver, meta.Description).Contains("%"), "Percent/unit composite format");
            template.Compile("{stat1} then {stat1}", meta.Description, catalog);
            Check(template.Render(resolver, meta.Description).Split('%').Length == 3, "Translation can reorder/repeat tokens");
            meta.Description.boldValues = true;
            Check(template.Render(resolver, meta.Description).Contains("<b>"), "TMP rich text styling");
            meta.Description.boldValues = false; meta.Description.insertions[0].binding.valueFormat = "";
            var row = new StatDisplayRow(); row.cells.Add(Cell(damage)); meta.StatDisplay.baseStats.rows.Add(row);
            panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StatBlocksPreviewWindow.PanelPath));
            var view = panel.GetComponent<StatBlocksView>(); view.Show(meta, resolver);
            textObject = new GameObject("Description fixture", typeof(RectTransform)); textObject.SetActive(false);
            var text = textObject.AddComponent<TextMeshProUGUI>(); var description = textObject.AddComponent<StatDescriptionView>();
            Ref(description, "source", view); Ref(description, "target", text); textObject.SetActive(true);
            Check(text.text == "Damage: 25.", "Reusable TMP component reads current source");
            var boxes = view.GetComponentsInChildren<StatBox>(); resolver.Damage = 30; view.RefreshValues();
            Check(text.text == "Damage: 30." && view.GetComponentsInChildren<StatBox>().SequenceEqual(boxes), "ValuesRefreshed updates description without rebuilding cards");
            view.Show(null); description.SetFallback(""); Check(text.text == "", "Clearing item clears description");
            view.Show(meta, resolver); textObject.SetActive(false); resolver.Damage = 40; view.RefreshValues(); textObject.SetActive(true);
            Check(text.text == "Damage: 40.", "Re-enable restores subscriptions/current values");

            // Every existing descriptor is accepted, including strings, enums, computed
            // values, nested projectiles and unique ability contracts: no category whitelist.
            foreach (string type in new[] { "WeaponMetaConfig", "ShipMetaConfig" })
            foreach (var guid in AssetDatabase.FindAssets("t:" + type))
            {
                var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                var all = StatCatalogBuilder.Build(source);
                foreach (var d in all.Values)
                {
                    var settings = new StatDescriptionSettings { text = "{value}" };
                    settings.insertions.Add(new StatDescriptionInsertion { token = "value", binding = Cell(d) });
                    template.Compile(settings.text, settings, all);
                    Check(template.Render(resolver, settings) == StatValueFormatter.Format(d, resolver, "", out _), "Descriptor parity: " + d.Id);
                    values++;
                }
            }
            var persistent = AssetDatabase.LoadAssetAtPath<WeaponMetaConfig>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:WeaponMetaConfig")[0]));
            var persistentValue = StatCatalogBuilder.Build(persistent).Values[0];
            var fixture = new StatDescriptionSettings { text = "{saved}" };
            fixture.insertions.Add(new StatDescriptionInsertion { token = "saved", binding = Cell(persistentValue) });
            meta.Description.insertions = fixture.insertions; meta.Description.text = fixture.text;
            var clone = ScriptableObject.CreateInstance<WeaponMetaConfig>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(meta), clone);
                Check(clone.Description.text == "{saved}" && clone.Description.insertions[0].binding.sourceAsset == persistentValue.SourceAsset, "Meta serialization preserves template and asset identity");
                clone.Description.insertions[0].binding.statId = "removed";
                var missing = new StatDescriptionTemplate(); missing.Compile(clone.Description.text, clone.Description, StatCatalogBuilder.Build(persistent));
                Check(missing.Render(null, clone.Description).Contains("[missing]"), "Missing stat never substitutes a different value");
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
            return new { passed = true, checks, descriptorsCompared = values, authoredMetaChanged = false };
        }
        finally
        {
            if (textObject != null) UnityEngine.Object.DestroyImmediate(textObject);
            if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
            UnityEngine.Object.DestroyImmediate(meta);
        }
    }
}
