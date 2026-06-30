using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "SubWaveBehaviour/GoTo")]
public class GoToMovement : SOStrategyMovement
{
    [SerializeField] Ease easeType = Ease.Linear;
    [SerializeField] Vector2 targetPosition;
    [SerializeField] float duration = 4f;
    public override Tween Play(Transform target)
    {
        Tween tween = target
            .DOMove(targetPosition, duration)
            .SetEase(easeType);

        return tween;
    }
}
