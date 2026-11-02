using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InspectCraftShipPreview
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static string Path(Transform t) => t == null ? null : t.parent == null ? "/" + t.name : Path(t.parent) + "/" + t.name;
    static object Field(object o, string n) => o.GetType().GetField(n, Flags)?.GetValue(o);
    public static object ConfigureScene()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/NewMainMenu.unity");
        if (!scene.isLoaded) throw new InvalidOperationException("NewMainMenu is not open");
        Transform screen = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Canvas") screen = root.transform.Find("ScreenRoot");
        var craft = screen.Find("CraftMenu"); var flow = craft.GetComponent<CraftCreationFlowController>();
        var preview = Field(flow, "hullPreview") as HullPreviewController;
        var duplicate = screen.Find("Hangar/HandlerTop/SelectedShip1st").GetComponent<HullPreviewController>();
        if (preview == null || duplicate == null || preview == duplicate) throw new Exception("Unexpected preview configuration");
        var rect = preview.GetComponent<RectTransform>(); var before = new Vector3[4]; rect.GetWorldCorners(before);
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Configure CraftMenu ship preview");
        try
        {
            var anchorObject = new GameObject("ShipPreviewAnchor", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create ship preview anchor");
            var anchor = anchorObject.GetComponent<RectTransform>();
            Undo.SetTransformParent(anchor, craft, "Parent ship preview anchor");
            Undo.RecordObject(anchor, "Position ship preview anchor");
            anchor.anchorMin = anchor.anchorMax = new Vector2(.5f, .5f); anchor.pivot = new Vector2(.5f, .5f);
            anchor.sizeDelta = rect.rect.size; anchor.localScale = Vector3.one; anchor.SetPositionAndRotation(rect.position, rect.rotation);
            Undo.SetTransformParent(rect, anchor, "Attach ship preview to anchor");
            Undo.RecordObject(rect, "Stretch preview inside anchor");
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity; rect.anchoredPosition3D = Vector3.zero;
            Undo.RecordObject(preview, "Assign preview anchor and scale");
            using (var so = new SerializedObject(preview))
            {
                so.FindProperty("previewAnchor").objectReferenceValue = anchor;
                so.FindProperty("previewScale").floatValue = 1;
                so.ApplyModifiedProperties();
            }
            Undo.DestroyObjectImmediate(duplicate);
            Canvas.ForceUpdateCanvases(); var after = new Vector3[4]; rect.GetWorldCorners(after);
            for (int i = 0; i < 4; i++) if (Vector3.Distance(before[i], after[i]) > .01f)
                throw new Exception("Preview layout moved unexpectedly when assigning the anchor");
            EditorUtility.SetDirty(preview); EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
            return new { scene = scene.path, anchor = Path(anchor), script = Path(preview.transform), previewScale = 1,
                duplicateControllerRemoved = true, originalPositionPreserved = true, saved = true };
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }
    public static object Show()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required");
        var canvas = GameObject.Find("Canvas");
        var craft = canvas.transform.Find("ScreenRoot/CraftMenu");
        var flow = craft.GetComponent<CraftCreationFlowController>();
        flow.BeginNewCraft();
        var preview = Field(flow, "hullPreview") as HullPreviewController;
        var hull = AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/ArcanoidContent.asset");
        typeof(CraftCreationFlowController).GetMethod("ApplyHullSelection", Flags)
            .Invoke(flow, new object[] { hull, hull.DefaultColorPalette });
        return Report();
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task<object> Verify()
    {
        Show(); await Task.Delay(100);
        var craft = GameObject.Find("Canvas").transform.Find("ScreenRoot/CraftMenu");
        var flow = craft.GetComponent<CraftCreationFlowController>();
        var preview = Field(flow, "hullPreview") as HullPreviewController;
        var anchor = Field(preview, "previewAnchor") as RectTransform;
        var image = Field(preview, "previewImage") as RawImage;
        var camera = Field(preview, "previewCamera") as Camera;
        var model = Field(preview, "previewInstance") as GameObject;
        int controllers = 0, previewCameras = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            controllers += root.GetComponentsInChildren<HullPreviewController>(true).Length;
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) if (c.name == "Hull Preview Camera") previewCameras++;
        }
        Check(controllers == 1 && previewCameras == 1, "Expected a single preview controller and camera");
        Check(anchor != null && preview.transform.parent == anchor, "Preview must stay attached to the explicit anchor");
        Check(flow.SelectedHull != null && flow.WeaponSlotCount > 0, "Selected hull and weapon slots initialized");
        float framing = camera.orthographicSize;
        using (var so = new SerializedObject(preview))
        {
            so.FindProperty("previewScale").floatValue = .5f; so.ApplyModifiedPropertiesWithoutUndo();
        }
        await Task.Delay(75);
        Check(Vector3.Distance(image.rectTransform.localScale, Vector3.one * .5f) < .001f, "Live scale setting did not resize preview");
        Check(Mathf.Abs(camera.orthographicSize - framing) < .001f, "Auto framing must not cancel requested display scale");
        var slots = (System.Collections.IList)Field(preview, "previewWeaponSlots");
        foreach (var slot in slots)
        {
            var button = Field(slot, "Button") as CraftWeaponSlotButton;
            Check(button != null && button.transform.parent == image.transform, "Weapon button must scale with ship surface");
        }
        Vector2 originalAnchor = anchor.anchoredPosition; Vector3 oldPosition = image.transform.position;
        anchor.anchoredPosition += new Vector2(80, 40); await Task.Delay(75);
        Check(Vector3.Distance(oldPosition, image.transform.position) > .01f, "Anchor movement did not move the ship surface");
        Check(Vector3.Distance(image.transform.position, anchor.position) < .01f, "Preview no longer centered on anchor");
        var other = AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/BladeContent.asset");
        preview.Show(other, other.DefaultColorPalette, flow);
        Check(!model.activeSelf, "Old hull remains visible in the replacement frame");
        await Task.Delay(75); Check(model == null, "Old hull was not destroyed");
        Check(Vector3.Distance(image.rectTransform.localScale, Vector3.one * .5f) < .001f, "Scale lost during hull replacement");
        anchor.anchoredPosition = originalAnchor;
        using (var so = new SerializedObject(preview)) { so.FindProperty("previewScale").floatValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); }
        var hull = AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/ArcanoidContent.asset");
        typeof(CraftCreationFlowController).GetMethod("ApplyHullSelection", Flags).Invoke(flow, new object[] { hull, hull.DefaultColorPalette });
        await Task.Delay(75);
        return new { passed = true, controllers, previewCameras, scaleUpdatesInPlayMode = true, anchorMovesShip = true,
            weaponButtonsScaleWithShip = true, replacementHidesOldHullImmediately = true, slots = flow.WeaponSlotCount,
            script = Path(preview.transform), anchor = Path(anchor) };
    }
    public static object Report()
    {
        var images = new List<object>(); var cameras = new List<object>(); var controllers = new List<object>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                cameras.Add(new { path = Path(camera.transform), camera.enabled, mask = camera.cullingMask,
                    target = camera.targetTexture != null ? camera.targetTexture.name : null, camera.orthographicSize });
            foreach (var preview in root.GetComponentsInChildren<HullPreviewController>(true))
            {
                var model = Field(preview, "previewInstance") as GameObject;
                controllers.Add(new { path = Path(preview.transform), preview.enabled, host = Path((Field(preview, "previewHost") as Transform)),
                    model = model != null ? Path(model.transform) : null, modelScale = model != null ? model.transform.lossyScale.ToString() : null });
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "SelectedShip1st") continue;
                images.Add(new { path = Path(image.transform), image.enabled, alpha = image.color.a, sprite = image.sprite != null ? image.sprite.name : null,
                    active = image.gameObject.activeInHierarchy, rect = image.rectTransform.rect.ToString() });
            }
        }
        var hulls = new List<object>();
        foreach (var guid in AssetDatabase.FindAssets("t:HullContentDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var hull = AssetDatabase.LoadAssetAtPath<HullContentDefinition>(path);
            if (hull.Prefab == null) continue;
            var sprites = new List<object>();
            foreach (var renderer in hull.Prefab.GetComponentsInChildren<SpriteRenderer>(true))
                sprites.Add(new { path = Path(renderer.transform), renderer.enabled, sprite = renderer.sprite != null ? renderer.sprite.name : null, size = renderer.bounds.size.ToString() });
            hulls.Add(new { path, prefab = AssetDatabase.GetAssetPath(hull.Prefab), sprites });
            if (hulls.Count == 3) break;
        }
        return new { playing = Application.isPlaying, images, cameras, controllers, hulls };
    }
}
