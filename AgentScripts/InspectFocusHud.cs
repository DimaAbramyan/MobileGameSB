using UnityEngine;

public static class InspectFocusHud
{
    public static object Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var hud = GameObject.Find("MainHUD");
        var bottom = hud != null ? hud.transform.Find("BottomHUD") : null;
        var rows = new System.Collections.Generic.List<object>();
        if (bottom != null)
        {
            foreach (Transform child in bottom)
            {
                var rt = child as RectTransform;
                rows.Add(new { name = child.name,components=System.Array.ConvertAll(child.GetComponents<Component>(),c=>c.GetType().Name), position = new[] {rt.anchoredPosition.x,rt.anchoredPosition.y},
                    size = new[] {rt.rect.width,rt.rect.height},anchor = new[] {rt.anchorMin.x,rt.anchorMin.y} });
            }
        }
        return new {scene=scene.path,dirty=scene.isDirty,children=rows};
    }
}
