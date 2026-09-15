using System.Collections;
using UnityEngine;

public sealed class PrismBeamVisual : MonoBehaviour
{
    private const float Duration = 0.15f;
    private const float Width = 0.16f;

    public static void Create(
        Vector3 origin,
        Vector3 end,
        Material material)
    {
        var visualObject = new GameObject("Prism Ability Beam");
        var visual = visualObject.AddComponent<PrismBeamVisual>();
        visual.Play(origin, end, material);
    }

    private void Play(Vector3 origin, Vector3 end, Material material)
    {
        LineRenderer renderer = gameObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.positionCount = 2;
        renderer.startWidth = Width;
        renderer.endWidth = Width;
        renderer.numCapVertices = 2;
        renderer.sortingOrder = 10;
        renderer.SetPosition(0, origin);
        renderer.SetPosition(1, end);
        if (material != null)
            renderer.sharedMaterial = material;

        StartCoroutine(Fade(renderer));
    }

    private static IEnumerator Fade(LineRenderer renderer)
    {
        Color color = new Color(0.35f, 0.9f, 1f, 0.95f);
        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(0.95f, 0f, elapsed / Duration);
            renderer.startColor = color;
            renderer.endColor = color;
            yield return null;
        }

        Destroy(renderer.gameObject);
    }
}
