using UnityEngine;

public sealed class ShipColorMaterialApplier : MonoBehaviour
{
    private static readonly int PrimaryColorId = Shader.PropertyToID("_PrimaryColor");
    private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");

    [SerializeField] private Renderer[] targetRenderers;

    private MaterialPropertyBlock propertyBlock;

    public void Apply(ShipColorPalette palette)
    {
        if (palette == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(PrimaryColorId, palette.primary);
            propertyBlock.SetColor(SecondaryColorId, palette.secondary);
            propertyBlock.SetColor(AccentColorId, palette.accent);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
