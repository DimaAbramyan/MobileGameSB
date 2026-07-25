using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public enum MovementCommandType
{
    SpawnAt,
    MoveLocal,
    MoveWorld,
    RotateBy,
    Repeat,
    Wait,
    DeactivateChildrenFor
}

[Serializable]
public sealed class MovementCommandData
{
    public MovementCommandType type;

    public Vector3 position;
    [Min(0f)] public float duration = 1f;
    public Ease ease = Ease.Linear;

    public float degrees;

    [Min(1)] public int fromAction = 1;
    [Min(1)] public int toAction = 1;
    [Min(0)] public int repeatCount = 1;
    public bool infinite;

    [Min(0f)] public float waitDuration = 1f;
    [Min(0f)] public float deactivateDuration = 1f;
}

public class MovementSequencePlayer : MonoBehaviour
{
    [Header("Command sequence")]
    [SerializeField] private MovementCommandData[] commands;

    [Header("Legacy SO sequence (fallback)")]
    [SerializeField] private SOStrategyMovement[] movements;

    private int currentIndex;
    private Tween currentTween;
    private Coroutine commandRoutine;
    private readonly System.Collections.Generic.List<ChildActiveState>
        hiddenChildren = new System.Collections.Generic.List<ChildActiveState>();

    private struct ChildActiveState
    {
        public GameObject gameObject;
        public bool wasActive;
    }

    void OnEnable()
    {
        if (commands != null && commands.Length > 0)
        {
            commandRoutine = StartCoroutine(ExecuteCommands());
            return;
        }

        currentIndex = 0;
        PlayNext();
    }

    void OnDisable()
    {
        if (commandRoutine != null)
        {
            StopCoroutine(commandRoutine);
            commandRoutine = null;
        }

        currentTween?.Kill();
        currentTween = null;
        RestoreChildren();
    }

    private void PlayNext()
    {
        if (currentIndex >= movements.Length)
            return;

        currentTween = movements[currentIndex].Play(transform);
        currentIndex++;

        currentTween.OnComplete(PlayNext);
    }

    public void SetCommands(MovementCommandData[] newCommands)
    {
        commands = newCommands;
    }

    private IEnumerator ExecuteCommands()
    {
        yield return ExecuteRange(0, commands.Length - 1, 0);
        currentTween = null;
        commandRoutine = null;
    }

    private IEnumerator ExecuteRange(int firstIndex, int lastIndex, int depth)
    {
        if (depth > 32)
        {
            Debug.LogError("Movement command nesting is too deep.", this);
            yield break;
        }

        for (int i = firstIndex; i <= lastIndex; i++)
        {
            MovementCommandData command = commands[i];
            if (command == null)
                continue;

            switch (command.type)
            {
                case MovementCommandType.SpawnAt:
                    transform.position = command.position;
                    break;

                case MovementCommandType.MoveLocal:
                    currentTween = transform
                        .DOLocalMove(command.position, command.duration)
                        .SetRelative()
                        .SetEase(command.ease);
                    yield return currentTween.WaitForCompletion();
                    break;

                case MovementCommandType.MoveWorld:
                    currentTween = transform
                        .DOMove(command.position, command.duration)
                        .SetEase(command.ease);
                    yield return currentTween.WaitForCompletion();
                    break;

                case MovementCommandType.RotateBy:
                    currentTween = transform
                        .DOLocalRotate(
                            new Vector3(0f, 0f, command.degrees),
                            command.duration,
                            RotateMode.LocalAxisAdd)
                        .SetEase(command.ease);
                    yield return currentTween.WaitForCompletion();
                    break;

                case MovementCommandType.Repeat:
                    yield return ExecuteRepeat(command, i, depth + 1);
                    break;

                case MovementCommandType.Wait:
                    yield return new WaitForSeconds(command.waitDuration);
                    break;

                case MovementCommandType.DeactivateChildrenFor:
                    yield return DeactivateChildrenFor(command.deactivateDuration);
                    break;
            }
        }

    }

    private IEnumerator ExecuteRepeat(
        MovementCommandData command,
        int repeatCommandIndex,
        int depth)
    {
        int firstIndex = command.fromAction - 1;
        int lastIndex = command.toAction - 1;

        if (firstIndex < 0
            || lastIndex < firstIndex
            || lastIndex >= repeatCommandIndex)
        {
            Debug.LogError(
                $"Invalid Repeat range {command.fromAction}–{command.toAction}. "
                + $"It must reference earlier actions before action {repeatCommandIndex + 1}.",
                this);
            yield break;
        }

        if (command.infinite)
        {
            while (true)
            {
                yield return ExecuteRange(firstIndex, lastIndex, depth);
                yield return null;
            }
        }

        for (int repeat = 0; repeat < command.repeatCount; repeat++)
            yield return ExecuteRange(firstIndex, lastIndex, depth);
    }

    private IEnumerator DeactivateChildrenFor(float duration)
    {
        RestoreChildren();

        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            bool wasActive = child.activeSelf;

            hiddenChildren.Add(new ChildActiveState
            {
                gameObject = child,
                wasActive = wasActive
            });

            if (wasActive)
                child.SetActive(false);
        }

        yield return new WaitForSeconds(duration);
        RestoreChildren();
    }

    private void RestoreChildren()
    {
        foreach (ChildActiveState state in hiddenChildren)
        {
            if (state.gameObject != null)
                state.gameObject.SetActive(state.wasActive);
        }

        hiddenChildren.Clear();
    }

    private void OnValidate()
    {
        if (commands == null)
            return;

        foreach (MovementCommandData command in commands)
        {
            if (command == null)
                continue;

            command.duration = Mathf.Max(0f, command.duration);
            command.waitDuration = Mathf.Max(0f, command.waitDuration);
            command.deactivateDuration = Mathf.Max(0f, command.deactivateDuration);
            command.fromAction = Mathf.Max(1, command.fromAction);
            command.toAction = Mathf.Max(command.fromAction, command.toAction);
            command.repeatCount = Mathf.Max(0, command.repeatCount);
        }
    }
}
