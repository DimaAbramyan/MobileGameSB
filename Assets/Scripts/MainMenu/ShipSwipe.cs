using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ShipSwipe : MonoBehaviour
{
    private int selectedShip;
    [SerializeField] private GameObject[] ships;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text activeAbilityDescriptionText;
    [SerializeField] private TMP_Text passiveAbilityDescriptionText;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text shieldValueText;
    [SerializeField] private TMP_Text weaponCountValueText;
    [SerializeField] private TMP_Text energyValueText;

    [Header("Stat Icons")]
    [SerializeField] private Image healthIconImage;
    [SerializeField] private Sprite healthIcon;
    [SerializeField] private Image shieldIconImage;
    [SerializeField] private Sprite shieldIcon;
    [SerializeField] private Image weaponCountIconImage;
    [SerializeField] private Sprite weaponCountIcon;
    [SerializeField] private Image energyIconImage;
    [SerializeField] private Sprite energyIcon;

    [SerializeField] private RadarChartGraphic statsChart;
    [SerializeField] private CraftEnergyIndicator craftEnergyIndicator;

    public BodyData SelectedBody =>
        ships != null
        && ships.Length > 0
        && selectedShip >= 0
        && selectedShip < ships.Length
        && ships[selectedShip] != null
            ? ships[selectedShip].GetComponent<BodyData>()
            : null;

    private Vector2 range;
    private Vector2 startPos;
    private Vector2 endPos;

    private void Start()
    {
        range.x = Screen.width / 10;
        range.y = Screen.height / 8;

        if (statsChart == null)
            statsChart = GetComponentInChildren<RadarChartGraphic>(true);
        if (statsChart == null)
            statsChart = FindFirstObjectByType<RadarChartGraphic>(
                FindObjectsInactive.Include);
        if (craftEnergyIndicator == null)
            craftEnergyIndicator =
                FindFirstObjectByType<CraftEnergyIndicator>(
                    FindObjectsInactive.Include);

        UpdateSelectedShipVisual();
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    startPos = touch.position;
                    break;
                case TouchPhase.Ended:
                    endPos = touch.position;
                    HandleSwipe();
                    break;
            }
        }
    }

    private void HandleSwipe()
    {
        if (!gameObject.activeSelf)
        {
            if ((Mathf.Abs(startPos.x - endPos.x) > range.x)
                && (Mathf.Abs(startPos.y - endPos.y) < range.y))
            {
                if (startPos.x - endPos.x > 0)
                {
                    SelectNext();
                }
                else
                {
                    SelectPrevious();
                }
            }
        }
    }

    public void SelectNext()
    {
        if (ships == null || ships.Length == 0)
            return;

        ships[selectedShip].SetActive(false); // Скрываем текущий объект
        selectedShip = (selectedShip + 1) % ships.Length; // Переходим к следующему объекту
        ships[selectedShip].SetActive(true); // Показываем следующий объект
        UpdateSelectedShipVisual();
    }

    public void SelectPrevious()
    {
        if (ships == null || ships.Length == 0)
            return;

        ships[selectedShip].SetActive(false); // Скрываем текущий объект
        selectedShip = (selectedShip - 1 + ships.Length) % ships.Length; // Переходим к предыдущему объекту
        ships[selectedShip].SetActive(true); // Показываем предыдущий объект
        UpdateSelectedShipVisual();
    }

    private void UpdateSelectedShipVisual()
    {
        ShipSelectionVisualConfig config = GetSelectedVisualConfig();
        ShipData shipData = config != null ? config.ShipData : null;

        SetText(shipNameText, config != null ? config.ShipName : string.Empty);
        SetText(
            activeAbilityDescriptionText,
            config != null ? config.ActiveAbilityDescription : string.Empty);
        SetText(
            passiveAbilityDescriptionText,
            config != null ? config.PassiveAbilityDescription : string.Empty);
        SetShipDataText(
            healthValueText,
            "Health",
            shipData != null ? shipData.maximumHealthPoints : null);
        SetIcon(healthIconImage, healthIcon, shipData != null);
        SetShipDataText(
            shieldValueText,
            "Shield",
            shipData != null ? shipData.maximumShieldPoints : null);
        SetIcon(shieldIconImage, shieldIcon, shipData != null);
        SetShipDataText(
            weaponCountValueText,
            "Weapons",
            shipData != null ? shipData.maximumWeaponCount : null);
        SetIcon(weaponCountIconImage, weaponCountIcon, shipData != null);
        SetShipDataText(
            energyValueText,
            "Energy",
            shipData != null ? shipData.maximumEnergy : null);
        SetIcon(energyIconImage, energyIcon, shipData != null);

        if (statsChart != null)
            statsChart.SetParameters(
                config != null ? config.RadarChartConfig : null,
                config != null ? config.RadarChartValues : null);

        if (craftEnergyIndicator != null)
            craftEnergyIndicator.Refresh(SelectedBody);
    }

    private ShipSelectionVisualConfig GetSelectedVisualConfig()
    {
        if (ships == null
            || ships.Length == 0
            || selectedShip < 0
            || selectedShip >= ships.Length
            || ships[selectedShip] == null)
        {
            return null;
        }

        BodyData selectedBody = SelectedBody;
        if (selectedBody == null)
        {
            return null;
        }

        return selectedBody.VisualConfig;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static void SetShipDataText(
        TMP_Text text,
        string label,
        float? value)
    {
        SetText(
            text,
            value.HasValue
                ? $"{label}: {value.Value:0.##}"
                : string.Empty);
    }

    private static void SetShipDataText(
        TMP_Text text,
        string label,
        int? value)
    {
        SetText(
            text,
            value.HasValue
                ? $"{label}: {value.Value}"
                : string.Empty);
    }

    private static void SetIcon(Image image, Sprite sprite, bool isVisible)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = isVisible && sprite != null;
    }
}
