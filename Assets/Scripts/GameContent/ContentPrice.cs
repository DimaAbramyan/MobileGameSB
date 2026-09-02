using System;
using UnityEngine;

[Serializable]
public struct ContentPrice
{
    [SerializeField, Min(0)] private int metal;
    [SerializeField, Min(0)] private int cores;

    public ContentPrice(int metal, int cores)
    {
        this.metal = Mathf.Max(0, metal);
        this.cores = Mathf.Max(0, cores);
    }

    public int Metal => Mathf.Max(0, metal);
    public int Cores => Mathf.Max(0, cores);
    public bool IsFree => Metal == 0 && Cores == 0;

    public string ToDisplayString()
    {
        if (IsFree)
            return "Бесплатно";

        if (Metal == 0)
            return $"{Cores} ядер";

        if (Cores == 0)
            return $"{Metal} металла";

        return $"{Metal} металла, {Cores} ядер";
    }

    public void Validate()
    {
        metal = Mathf.Max(0, metal);
        cores = Mathf.Max(0, cores);
    }
}
