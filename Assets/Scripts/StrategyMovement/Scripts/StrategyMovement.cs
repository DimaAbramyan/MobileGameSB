using DG.Tweening;
using UnityEngine;

//x-2.25
//y-5
public abstract class SOStrategyMovement : ScriptableObject
{
    public abstract Tween Play(Transform target);
}
