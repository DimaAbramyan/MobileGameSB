using DG.Tweening;
using UnityEngine;

public class MovementSequencePlayer : MonoBehaviour
{
    [SerializeField] private SOStrategyMovement[] movements;
    private int currentIndex;
    private Tween currentTween;

    void OnEnable()
    {
        currentIndex = 0;
        PlayNext();
    }

    void OnDisable()
    {
        Debug.Log("MovementSequencePlayer DISABLED");
        currentTween?.Kill();
    }

    private void PlayNext()
    {
        if (currentIndex >= movements.Length)
            return;

        currentTween = movements[currentIndex].Play(transform);
        currentIndex++;

        currentTween.OnComplete(PlayNext);
    }
}
