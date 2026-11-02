using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public static class VerifyStatBlocksScene
{
    static int checks;
    static readonly List<UnityEngine.Object> temporary = new();
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue; f.SetValue(target, value); return;
        }
        throw new Exception("Unknown fixture field: " + field);
    }
    static Transform Find(string path)
    {
        var pieces = path.Split('/'); var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(r => r.name == pieces[0]).transform;
        return root.Find(path.Substring(pieces[0].Length + 1));
    }
    static Button Selectable(CraftUIButton button) => button.GetComponentInChildren<Button>(true);
    static void Hover(CraftUIButton button, bool enter)
    {
        var data = new PointerEventData(EventSystem.current);
        if (enter) ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
        else ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
    }
    static bool Text(StatBlocksView view, string text) => view.GetComponentsInChildren<TMP_Text>().Any(t => t.text == text);
    static StatDisplayCell Damage(WeaponMetaConfig meta)
    {
        var definition = StatCatalogBuilder.Build(meta).Values.Single(d => d.ProviderType == typeof(WeaponMetaBasicStats) && d.Property == "Damage");
        return new StatDisplayCell { useBinding = true, sourcePath = definition.SourcePath, statId = definition.Id, sourceAsset = definition.SourceAsset };
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Run in the existing NewMainMenu Play Mode.");
        checks = 0;
        var progress = ProjectContext.Instance.Container.Resolve<ContentProgressService>();
        var craft = Find("Canvas/ScreenRoot/CraftMenu"); var flow = craft.GetComponent<CraftCreationFlowController>();
        var hullInfo = craft.Find("SelectBody/HandlerTop/InfoBody").GetComponent<HullSelectionInfoController>();
        var weaponInfo = craft.Find("SelectWeapon/HandlerTop/InfoWeapon").GetComponent<WeaponSelectionInfoController>();
        string fixtureId = "stat-ui-check-" + Guid.NewGuid().ToString("N");
        var entries = (Dictionary<string, ContentProgressEntry>)typeof(ContentProgressService).GetField("entriesById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(progress);
        try
        {
            Check(hullInfo.StatBlocks != null && weaponInfo.StatBlocks != null, "Scene Inspector references assigned");
            foreach (var panel in new[] { hullInfo.StatBlocks, weaponInfo.StatBlocks })
            {
                Check(((RectTransform)panel.transform).rect.width > 500, "Panel fits existing info column");
                Check(panel.transform.parent.GetComponent<ScrollRect>().content == panel.transform, "Existing panel is scroll content");
            }
            flow.BeginNewCraft();
            await Task.Delay(150);
            var hullButtons = craft.Find("SelectBody/HandlerBot/Crafts/Viewport/Content").GetComponentsInChildren<CraftUIButton>();
            Check(hullButtons.Length > 1, "Actual hull list populated");
            var hullA = hullButtons.First(b => b.Content is HullContentDefinition h && h.Data?.ShipMetaConfig != null && progress.IsOwned(h));
            var hullB = hullButtons.First(b => b != hullA && b.Content is HullContentDefinition h && h.Data?.ShipMetaConfig != null);
            Selectable(hullA).onClick.Invoke();
            Check(flow.SelectedHull == hullA.Content && hullInfo.StatBlocks.Meta == ((HullContentDefinition)hullA.Content).Data.ShipMetaConfig, "Hull click uses existing permanent selection");
            Hover(hullB, true);
            Check(hullInfo.DisplayedHull == hullB.Content && flow.SelectedHull == hullA.Content, "Hull hover is temporary");
            Hover(hullB, false);
            Check(hullInfo.DisplayedHull == hullA.Content, "Hull pointer exit restores selected hull");
            EventSystem.current.SetSelectedGameObject(Selectable(hullB).gameObject);
            Check(hullInfo.DisplayedHull == hullB.Content, "Actual child Selectable forwards navigation focus");
            Hover(hullA, true); Hover(hullA, false);
            Check(hullInfo.DisplayedHull == hullB.Content, "Pointer exit restores keyboard focus before permanent selection");
            EventSystem.current.SetSelectedGameObject(null);
            Check(hullInfo.DisplayedHull == hullA.Content, "Navigation exit restores selected hull");
            Hover(hullB, true); hullB.gameObject.SetActive(false);
            Check(hullInfo.DisplayedHull == hullA.Content, "Disabled hover button releases its preview");
            hullB.gameObject.SetActive(true);
            flow.SetSelectedHull(null);
            Check(hullInfo.DisplayedHull == null && hullInfo.StatBlocks.Meta == null, "No focus or hull clears panel");
            Selectable(hullA).onClick.Invoke();

            flow.ShowWeaponSelection();
            await Task.Delay(150);
            var weaponRoot = craft.Find("SelectWeapon/HandlerBot/Crafts/Viewport/Content");
            var weaponButtons = weaponRoot.GetComponentsInChildren<CraftUIButton>();
            Check(weaponButtons.Length > 1, "Actual weapon list populated");
            var weaponA = weaponButtons.First(b => b.Content is WeaponContentDefinition w && w.Data?.WeaponMetaConfig != null && progress.IsOwned(w));
            var weaponB = weaponButtons.First(b => b != weaponA && b.Content is WeaponContentDefinition w && w.Data?.WeaponMetaConfig != null);
            flow.SelectWeapon((WeaponContentDefinition)weaponA.Content);
            Check(weaponInfo.DisplayedWeapon == weaponA.Content, "Weapon focus event shows selected weapon");
            Hover(weaponB, true);
            Check(weaponInfo.StatBlocks.Meta == ((WeaponContentDefinition)weaponB.Content).Data.WeaponMetaConfig && flow.FocusedWeapon == weaponA.Content, "Weapon hover switches only info");
            Hover(weaponB, false);
            Check(weaponInfo.DisplayedWeapon == weaponA.Content, "Weapon exit restores focused weapon");
            EventSystem.current.SetSelectedGameObject(Selectable(weaponB).gameObject);
            Check(weaponInfo.DisplayedWeapon == weaponB.Content, "Weapon navigation focus supported");
            EventSystem.current.SetSelectedGameObject(null);
            Hover(weaponB, true);
            weaponB.SetContent(weaponB.Content);
            Check(weaponInfo.DisplayedWeapon == weaponA.Content, "Rebinding a reused button releases obsolete preview");

            // The real scene panel and real ProgressChanged subscription, using only transient
            // fixture content. No purchase, resource spending, Save(), or authored-asset edits.
            var meta = ScriptableObject.CreateInstance<WeaponMetaConfig>(); temporary.Add(meta);
            Set(meta.GetLevel(0).BasicStats, "damage", 20f); meta.SetMaxLevels(2); Set(meta.GetLevel(1).BasicStats, "damage", 25f);
            var row = new StatDisplayRow(); row.cells.Add(Damage(meta)); meta.StatDisplay.baseStats.rows.Add(row);
            meta.Description.text = "Deals {damage} damage";
            meta.Description.insertions.Add(new StatDescriptionInsertion { token = "damage", binding = Damage(meta) });
            var data = UnityEngine.Object.Instantiate(((WeaponContentDefinition)weaponA.Content).Data); temporary.Add(data); Set(data, "weaponMetaConfig", meta);
            var content = UnityEngine.Object.Instantiate((WeaponContentDefinition)weaponA.Content); temporary.Add(content);
            Set(content, "data", data); Set(content, "id", fixtureId); Set(content, "isStarterUnlocked", true); Set(content, "maxUpgradeLevel", 1);
            entries.Add(fixtureId, new ContentProgressEntry { contentId = fixtureId, isOwned = true, upgradeLevel = 0 });
            var fixture = UnityEngine.Object.Instantiate(weaponA, weaponRoot, false); temporary.Add(fixture.gameObject);
            fixture.transform.localPosition = Vector3.one * 100000f;
            fixture.SetContent(content); fixture.SetPreviewActions(weaponInfo.Preview, weaponInfo.EndPreview);
            Hover(fixture, true);
            Check(Text(weaponInfo.StatBlocks, "20"), "Current persistent level shown");
            Check(weaponInfo.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "Deals 20 damage"), "Scene description switches to focused Meta");
            var boxes = weaponInfo.StatBlocks.GetComponentsInChildren<StatBox>();
            entries[fixtureId].upgradeLevel = 1;
            Check(progress.GetUpgradeLevel(content) == 1, "Starter content preserves saved upgrade level");
            ((Action)typeof(ContentProgressService).GetField("ProgressChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(progress))?.Invoke();
            var infoProgress = (ContentProgressService)typeof(WeaponSelectionInfoController).GetField("contentProgressService", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weaponInfo);
            Check(Text(weaponInfo.StatBlocks, "25"), "Upgrade update: injectedService=" + (infoProgress != null) + "; sameService=" + ReferenceEquals(infoProgress, progress)
                + "; shown=" + weaponInfo.DisplayedWeapon?.Id + "; expected=" + content.Id + "; values=" + string.Join(",", weaponInfo.StatBlocks.GetComponentsInChildren<TMP_Text>().Select(t => t.text)));
            Check(weaponInfo.StatBlocks.GetComponentsInChildren<StatBox>().SequenceEqual(boxes), "Upgrade preserves grid cell instances");
            Check(weaponInfo.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "Deals 25 damage"), "Scene description updates together with cards after upgrade");
            Check(weaponRoot.GetComponentsInChildren<CraftUIButton>().Contains(weaponA), "Progress update preserves existing list buttons and hover");
            fixture.gameObject.SetActive(false);
            Check(weaponInfo.DisplayedWeapon == weaponA.Content, "Temporary item removal restores permanent weapon");
            Check(!weaponInfo.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "Deals 25 damage"), "Focus exit removes obsolete description");
            flow.SelectWeapon(null);
            Check(weaponInfo.StatBlocks.Meta == null, "Cleared weapon focus leaves no stale stats");
            flow.SelectWeapon((WeaponContentDefinition)weaponA.Content);
            return new { passed = true, checks, scene = SceneManager.GetActiveScene().path,
                hulls = new[] { hullA.Content.DisplayName, hullB.Content.DisplayName }, weapons = new[] { weaponA.Content.DisplayName, weaponB.Content.DisplayName },
                upgrade = "20 -> 25 (same cell instances)", modifiedSaveData = false, modifiedAuthoredMeta = false,
                actualWeaponCells = weaponInfo.StatBlocks.GetComponentsInChildren<StatBox>().Length };
        }
        finally
        {
            entries.Remove(fixtureId);
            foreach (var item in temporary) if (item != null) UnityEngine.Object.Destroy(item);
            temporary.Clear();
        }
    }
}
