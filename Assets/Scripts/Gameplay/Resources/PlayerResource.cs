using System;
using UnityEngine;

public sealed class PlayerResource
{
    public PlayerResource(ResourceDefinition definition, int amount = 0)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Amount = Mathf.Max(0, amount);
    }

    public ResourceDefinition Definition { get; }
    public ResourceKind Kind => Definition.Kind;
    public string DisplayName => Definition.DisplayName;
    public Sprite Icon => Definition.Icon;
    public int Amount { get; private set; }

    public void Add(int amount)
    {
        Amount += Mathf.Max(0, amount);
    }

    public bool TrySpend(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (Amount < cost)
            return false;

        Amount -= cost;
        return true;
    }

    public void SetAmount(int amount)
    {
        Amount = Mathf.Max(0, amount);
    }
}
