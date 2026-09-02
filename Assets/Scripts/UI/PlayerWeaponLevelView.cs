using TMPro;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(TMP_Text))]
public sealed class PlayerWeaponLevelView : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string levelFormat = "{0}";
    [SerializeField] private string maxText = "MAX";

    private PlayerController playerController;
    private ParentShip observedShip;

    [Inject]
    private void Construct(PlayerController controller)
    {
        playerController = controller;
    }

    private void Awake()
    {
        if (levelText == null)
            levelText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (playerController != null)
            SubscribeToPlayer();
    }

    private void Start()
    {
        SubscribeToPlayer();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayer();
        SetObservedShip(null);
    }

    private void SubscribeToPlayer()
    {
        if (playerController == null)
            return;

        playerController.OnCurrentShipChanged -= OnCurrentShipChanged;
        playerController.OnCurrentShipChanged += OnCurrentShipChanged;
        SetObservedShip(playerController.CurrentShip);
    }

    private void UnsubscribeFromPlayer()
    {
        if (playerController != null)
            playerController.OnCurrentShipChanged -= OnCurrentShipChanged;
    }

    private void OnCurrentShipChanged(ParentShip ship)
    {
        SetObservedShip(ship);
    }

    private void SetObservedShip(ParentShip ship)
    {
        if (observedShip == ship)
        {
            Refresh();
            return;
        }

        if (observedShip != null)
            observedShip.OnLevelChanged -= OnLevelChanged;

        observedShip = ship;

        if (observedShip != null)
            observedShip.OnLevelChanged += OnLevelChanged;

        Refresh();
    }

    private void OnLevelChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (levelText == null)
            return;

        if (observedShip == null)
        {
            levelText.text = string.Empty;
            return;
        }

        levelText.text = observedShip.IsWeaponLevelMax
            ? maxText
            : string.Format(levelFormat, observedShip.GetLevel());
    }
}
