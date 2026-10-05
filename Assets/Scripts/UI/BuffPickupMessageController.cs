using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class BuffPickupMessageController : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [Header("World-space animation")]
    [FormerlySerializedAs("visibleDuration")]
    [SerializeField, Min(0f)] private float stationaryDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float upwardSpeed = 1f;
    [SerializeField] private float verticalOffset = 0.35f;
    [SerializeField] private Color textColor = new(0.72f, 1f, 0.82f, 1f);

    private Vector3 startPosition;
    private float elapsedTime;

    public void Configure(TMP_Text text)
    {
        messageText = text;
        Hide();
    }

    public void Show(string message, Vector3 pickupPosition)
    {
        if (string.IsNullOrWhiteSpace(message) || messageText == null)
            return;

        messageText.SetText(message);
        startPosition = pickupPosition + Vector3.up * verticalOffset;
        elapsedTime = 0f;
        messageText.transform.position = startPosition;
        messageText.color = textColor;
        if (!messageText.gameObject.activeSelf)
            messageText.gameObject.SetActive(true);

        enabled = true;
    }

    private void Awake()
    {
        Hide();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime <= stationaryDuration)
            return;

        float fadeElapsed = elapsedTime - stationaryDuration;
        float fadeProgress = Mathf.Clamp01(fadeElapsed / fadeDuration);
        messageText.transform.position = startPosition
            + Vector3.up * (upwardSpeed * fadeElapsed);

        Color color = textColor;
        color.a *= 1f - fadeProgress;
        messageText.color = color;

        if (fadeProgress >= 1f)
            Hide();
    }

    private void Hide()
    {
        elapsedTime = 0f;
        if (messageText != null && messageText.gameObject.activeSelf)
            messageText.gameObject.SetActive(false);

        enabled = false;
    }

    private void OnValidate()
    {
        stationaryDuration = Mathf.Max(0f, stationaryDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        upwardSpeed = Mathf.Max(0f, upwardSpeed);
    }
}
