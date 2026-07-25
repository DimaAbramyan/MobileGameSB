using System.Collections;
using TMPro;
using UnityEngine;

public sealed class WaveProgressPopup : MonoBehaviour
{
    private static WaveProgressPopup scenePopup;

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private string format = "Волна {0} из {1}";
    [SerializeField, Min(0f)] private float visibleDuration = 1.5f;
    [SerializeField] private bool hideAfterDelay = true;

    private Coroutine hideRoutine;

    public static WaveProgressPopup FindScenePopup()
    {
        if (scenePopup != null)
            return scenePopup;

#if UNITY_2023_1_OR_NEWER
        scenePopup = FindFirstObjectByType<WaveProgressPopup>(
            FindObjectsInactive.Include);
#else
        scenePopup = FindObjectOfType<WaveProgressPopup>(true);
#endif
        return scenePopup;
    }

    private void Awake()
    {
        EnsureReferences();
        scenePopup = this;

        Hide();
    }

    private void OnEnable()
    {
        scenePopup = this;
    }

    private void OnDestroy()
    {
        if (scenePopup == this)
            scenePopup = null;
    }

    public void Show(int current, int total)
    {
        EnsureReferences();

        if (progressText != null)
            progressText.text = string.Format(format, current, total);

        if (root != null)
            root.SetActive(true);

        if (!hideAfterDelay)
            return;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    public IEnumerator ShowAndWait(int current, int total)
    {
        bool previousHideAfterDelay = hideAfterDelay;
        hideAfterDelay = false;

        Show(current, total);

        yield return new WaitForSeconds(visibleDuration);
        hideAfterDelay = previousHideAfterDelay;
        Hide();
    }

    public void Hide()
    {
        EnsureReferences();

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (root != null)
            root.SetActive(false);
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleDuration);
        hideRoutine = null;

        if (root != null)
            root.SetActive(false);
    }

    private void EnsureReferences()
    {
        if (root == null)
            root = gameObject;

        if (progressText == null)
            progressText = GetComponentInChildren<TMP_Text>(true);
    }
}
