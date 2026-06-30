using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "SubWaveBehaviour/Circle")]
public class PatrolMovement : SOStrategyMovement
{
    [SerializeField] Ease easeType = Ease.Linear;
    [SerializeField] float radius;
    [SerializeField] int times;
    [SerializeField] bool isLooped;
    [SerializeField] float duration = 4f;
    public override Tween Play(Transform target)
    {
        if (isLooped)
            times = -1;

        Tween tween = target
            .DOMoveX(radius, duration)
            .SetRelative()               
            .SetEase(easeType)
            .SetLoops(times, LoopType.Yoyo);

        return tween;
    }
}
