using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Sprite geometry and authored renderer transforms, shared by static and live Wave overlays.
// No preview GameObjects or independent simulation are created here.
internal static class WaveEnemySilhouette
{
    private static readonly Dictionary<Sprite, Mesh> meshes = new();
    private static Material material;
    private static readonly Plane[] planes = new Plane[6];
    internal static void BeginScene(Camera camera) => GeometryUtility.CalculateFrustumPlanes(camera, planes);
    internal static Mesh MeshFor(Sprite sprite)
    {
        if (sprite == null) return null;
        if (meshes.TryGetValue(sprite, out var mesh) && mesh != null) return mesh;
        var points = sprite.vertices;
        var vertices = new Vector3[points.Length];
        var colors = new Color32[points.Length];
        for (int i = 0; i < points.Length; i++) { vertices[i] = points[i]; colors[i] = new Color32(255,255,255,255); }
        var indices = sprite.triangles;
        var triangles = new int[indices.Length];
        for (int i = 0; i < indices.Length; i++) triangles[i] = indices[i];
        mesh = new Mesh { name = "Wave enemy silhouette", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.uv = sprite.uv; mesh.triangles = triangles; mesh.colors32 = colors;
        mesh.RecalculateBounds(); meshes[sprite] = mesh;
        return mesh;
    }
    internal static Matrix4x4 RendererMatrix(SpriteRenderer renderer, Matrix4x4 matrix)
    {
        Vector3 scale = new(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1);
        if (renderer.drawMode != SpriteDrawMode.Simple && renderer.sprite != null)
        {
            Vector3 size = renderer.sprite.bounds.size;
            if (size.x > 0) scale.x *= renderer.size.x / size.x;
            if (size.y > 0) scale.y *= renderer.size.y / size.y;
        }
        return matrix * Matrix4x4.Scale(scale);
    }
    internal static bool NeedsOverlay(Renderer renderer)
        => renderer is SpriteRenderer && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.forceRenderingOff;

    internal static void Draw(SpriteRenderer renderer, Matrix4x4 matrix)
    {
        var mesh = MeshFor(renderer.sprite);
        if (mesh == null) return;
        matrix = RendererMatrix(renderer, matrix);
        var local = mesh.bounds;
        Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
        Vector3 y = matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
        Vector3 z = matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
        Vector3 extents = new(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
        if (!GeometryUtility.TestPlanesAABB(planes,new Bounds(matrix.MultiplyPoint3x4(local.center),extents*2))) return;
        if (material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        material.SetTexture("_MainTex", renderer.sprite.texture);
        material.SetColor("_Color", new Color(.25f,.95f,1f,.55f));
        if (material.SetPass(0)) Graphics.DrawMeshNow(mesh, matrix);
    }
    internal static void Clear()
    {
        foreach (var mesh in meshes.Values) if (mesh != null) Object.DestroyImmediate(mesh);
        meshes.Clear();
        if (material != null) Object.DestroyImmediate(material);
        material = null;
    }
}
