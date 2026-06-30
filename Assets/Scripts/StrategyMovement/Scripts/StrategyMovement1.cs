using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(menuName = "SubWaveBehaviour/ForvardLoop")]
public class ForvardLoop : SOStrategyMovement
{
    [SerializeField] Ease easeType = Ease.Linear;
    [SerializeField] float forvardMove;
    [SerializeField] float duration = 4f;

    public override Tween Play(Transform target)
    {
        Vector3 start = target.position;
        Vector3 end = start + new Vector3(0, -forvardMove, 0);

        Sequence sequence = DOTween.Sequence();

        sequence.Append(target.DOMove(end, duration).SetEase(easeType))
                .AppendCallback(() => target.position = start)
                .SetLoops(-1);

        return sequence;
    }
}
