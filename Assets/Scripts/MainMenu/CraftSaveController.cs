using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class CraftSaveController : MonoBehaviour
{
    private static readonly string[] NameAdjectives =
    {
        "Стальной", "Серебряный", "Звездный", "Северный",
        "Тихий", "Янтарный", "Багровый", "Лазурный",
        "Грозовой", "Полярный", "Смелый", "Скрытный",
        "Быстрый", "Темный", "Светлый", "Небесный"
    };

    private static readonly string[] NameNouns =
    {
        "Вихрь", "Страж", "Фантом", "Клинок",
        "Ястреб", "Дракон", "Шторм", "Импульс",
        "Рейдер", "Призрак", "Корсар", "Пилигрим",
        "Авангард", "Сокол", "Зенит", "Метеор"
    };

    [SerializeField] private CraftCreationFlowController craftCreationFlow;
    [SerializeField] private SavedCraftListController savedCraftList;
    [SerializeField] private Button finishButton;
    [SerializeField] private Button[] finishButtons;
    [SerializeField] private Button randomNameButton;
    [SerializeField] private RectTransform dialogHost;
    [SerializeField] private TMP_InputField craftNameInput;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject saveDialog;
    [SerializeField] private bool createDefaultDialogWhenUnconfigured = true;

    private SaveManager saveManager;
    [InjectOptional] private TeamSelectionService teamSelectionService;
    private TMP_Text craftNamePlaceholder;
    private string suggestedName;

    [Inject]
    private void Construct(SaveManager saveManager)
    {
        this.saveManager = saveManager;
    }

    private void Awake()
    {
        ResolveProjectDependencies();

        if (finishButton == null && (finishButtons == null || finishButtons.Length == 0))
            finishButton = GetComponent<Button>();

        if (!BindFinishButtons())
        {
            Debug.LogError("Craft save requires at least one Finish button.", this);
            enabled = false;
            return;
        }

        EnsureDialog();
        BindRandomNameButton();
        CloseSaveDialog();
    }

    private void OnDestroy()
    {
        if (finishButton != null)
            finishButton.onClick.RemoveListener(OpenSaveDialog);

        if (finishButtons != null)
        {
            for (int i = 0; i < finishButtons.Length; i++)
            {
                if (finishButtons[i] != null)
                    finishButtons[i].onClick.RemoveListener(OpenSaveDialog);
            }
        }

        if (craftNameInput != null)
            craftNameInput.onValueChanged.RemoveListener(ValidateEnteredName);

        if (randomNameButton != null)
            randomNameButton.onClick.RemoveListener(UseRandomName);
    }

    public void OpenSaveDialog()
    {
        ResolveProjectDependencies();

        if (!EnsureDialog())
            return;

        bool isEditingCraft = craftCreationFlow != null && craftCreationFlow.IsEditingCraft;
        string editingName = isEditingCraft ? craftCreationFlow.EditingCraftName : string.Empty;
        craftNameInput.SetTextWithoutNotify(editingName);
        SetSuggestedName(isEditingCraft ? null : CreateAvailableRandomName());
        ValidateEnteredName(editingName);
        saveDialog.SetActive(true);
        craftNameInput.ActivateInputField();
    }

    public void CloseSaveDialog()
    {
        if (saveDialog != null)
            saveDialog.SetActive(false);
    }

    public void SaveCurrentCraft()
    {
        ResolveProjectDependencies();

        if (!EnsureDialog())
            return;

        if (!TryCreateSaveShip(out SaveShip ship, out string error))
        {
            SetFeedback(error, true);
            return;
        }

        if (saveManager == null)
        {
            SetFeedback("Не удалось получить менеджер сохранений.", true);
            return;
        }

        bool updatesEditedCraft = craftCreationFlow != null
            && craftCreationFlow.IsEditingCraft;
        if (updatesEditedCraft)
        {
            ship.shipName = craftCreationFlow.EditingCraftName;
            saveManager.SaveShip(ship);
        }
        else if (!saveManager.TrySaveNewShip(ship, out error))
        {
            SetFeedback(error, true);
            return;
        }

        teamSelectionService?.UpdateSelectedShip(ship);
        savedCraftList?.Refresh();
        Debug.Log($"Craft '{ship.shipName}' was saved.", this);
        CloseSaveDialog();
        craftCreationFlow.CompleteNewCraft();
    }

    private bool TryCreateSaveShip(out SaveShip ship, out string error)
    {
        ship = null;
        if (craftCreationFlow == null)
        {
            error = "Не настроен контроллер создания крафта.";
            return false;
        }

        HullContentDefinition hull = craftCreationFlow.SelectedHull;
        if (hull == null || hull.Data == null)
        {
            error = "Сначала выберите корпус корабля.";
            return false;
        }

        if (!craftCreationFlow.TryCreateWeaponSaveData(out WeaponDataSer[] weapons, out error))
            return false;

        if (!ShipBuildValidator.TryValidate(
                hull.Data,
                weapons,
                out error,
                craftCreationFlow.WeaponSlotCount))
        {
            return false;
        }

        ShipColorPalette palette = craftCreationFlow.SelectedColorPalette
            ?? hull.DefaultColorPalette;

        string craftName = GetNameForSave(out error);
        if (string.IsNullOrEmpty(craftName))
            return false;

        ship = new SaveShip(
            hull.Data.shipId,
            weapons,
            craftName,
            string.Empty)
        {
            hullContentId = hull.Id,
            colors = palette
        };

        return true;
    }

    private void ValidateEnteredName(string value)
    {
        ResolveProjectDependencies();

        if (saveManager == null || string.IsNullOrWhiteSpace(value))
        {
            SetFeedback(string.Empty, false);
            return;
        }

        if (craftCreationFlow != null
            && craftCreationFlow.IsEditingCraft
            && string.Equals(
                value.Trim(),
                craftCreationFlow.EditingCraftName,
                System.StringComparison.Ordinal))
        {
            SetFeedback("Будет обновлён существующий крафт.", false);
            return;
        }

        if (saveManager.TryValidateNewShipName(value, out _, out string error))
            SetFeedback("Название доступно.", false);
        else
            SetFeedback(error, true);
    }

    private bool EnsureDialog()
    {
        if (craftNameInput != null && feedbackText != null && saveDialog != null)
        {
            CacheNamePlaceholder();
            return true;
        }

        if (!createDefaultDialogWhenUnconfigured || dialogHost == null)
        {
            Debug.LogError(
                "Assign the craft name input, feedback text, and dialog, or configure a dialog host.",
                this);
            return false;
        }

        CreateDefaultDialog();
        CacheNamePlaceholder();
        return craftNameInput != null && feedbackText != null && saveDialog != null;
    }

    private void CreateDefaultDialog()
    {
        GameObject dialogObject = new GameObject(
            "Craft Save Dialog",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        dialogObject.SetActive(false);
        RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.SetParent(dialogHost, false);
        dialogRect.SetAsLastSibling();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(520f, 280f);
        dialogObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.98f);

        CreateText(
            "Title",
            dialogRect,
            "Название крафта",
            32f,
            new Vector2(0f, 86f),
            new Vector2(450f, 44f),
            Color.white);

        CreateNameInput(dialogRect);

        feedbackText = CreateText(
            "Feedback",
            dialogRect,
            string.Empty,
            20f,
            new Vector2(0f, -26f),
            new Vector2(450f, 42f),
            new Color(0.65f, 0.9f, 0.95f, 1f));

        CreateDialogButton(
            "Save",
            dialogRect,
            "Сохранить",
            new Vector2(-92f, -96f),
            new Color(0.12f, 0.55f, 0.72f, 1f),
            SaveCurrentCraft);
        CreateDialogButton(
            "Cancel",
            dialogRect,
            "Отмена",
            new Vector2(92f, -96f),
            new Color(0.28f, 0.31f, 0.34f, 1f),
            CloseSaveDialog);

        saveDialog = dialogObject;
    }

    private void CreateNameInput(RectTransform parent)
    {
        GameObject inputObject = new GameObject(
            "Craft Name Input",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField));
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.SetParent(parent, false);
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(-31f, 26f);
        inputRect.sizeDelta = new Vector2(378f, 54f);

        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.94f, 0.96f, 0.97f, 1f);

        TextMeshProUGUI text = CreateText(
            "Text",
            inputRect,
            string.Empty,
            24f,
            Vector2.zero,
            Vector2.zero,
            new Color(0.08f, 0.1f, 0.13f, 1f));
        StretchWithPadding(text.rectTransform, 14f, 8f);

        TextMeshProUGUI placeholder = CreateText(
            "Placeholder",
            inputRect,
            "Введите уникальное название",
            24f,
            Vector2.zero,
            Vector2.zero,
            new Color(0.35f, 0.39f, 0.42f, 1f));
        StretchWithPadding(placeholder.rectTransform, 14f, 8f);

        craftNameInput = inputObject.GetComponent<TMP_InputField>();
        craftNameInput.targetGraphic = background;
        craftNameInput.textViewport = inputRect;
        craftNameInput.textComponent = text;
        craftNameInput.placeholder = placeholder;
        craftNameInput.lineType = TMP_InputField.LineType.SingleLine;
        craftNameInput.characterLimit = 48;
        craftNameInput.onValueChanged.AddListener(ValidateEnteredName);
        craftNameInput.onSubmit.AddListener(_ => SaveCurrentCraft());
        craftNamePlaceholder = placeholder;

        randomNameButton = CreateRandomNameButton(parent, new Vector2(193f, 26f));
    }

    public void UseRandomName()
    {
        if (!EnsureDialog())
            return;

        string randomName = CreateAvailableRandomName();
        if (string.IsNullOrEmpty(randomName))
        {
            SetFeedback("Не удалось подобрать свободное случайное название.", true);
            return;
        }

        SetSuggestedName(randomName);
        craftNameInput.SetTextWithoutNotify(randomName);
        ValidateEnteredName(randomName);
        craftNameInput.ActivateInputField();
    }

    private void BindRandomNameButton()
    {
        if (randomNameButton == null)
            return;

        randomNameButton.onClick.RemoveListener(UseRandomName);
        randomNameButton.onClick.AddListener(UseRandomName);
    }

    private bool BindFinishButtons()
    {
        bool hasFinishButton = false;

        BindFinishButton(finishButton, ref hasFinishButton);

        if (finishButtons != null)
        {
            for (int i = 0; i < finishButtons.Length; i++)
                BindFinishButton(finishButtons[i], ref hasFinishButton);
        }

        return hasFinishButton;
    }

    private void BindFinishButton(Button button, ref bool hasFinishButton)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(OpenSaveDialog);
        button.onClick.AddListener(OpenSaveDialog);
        hasFinishButton = true;
    }

    private void CacheNamePlaceholder()
    {
        if (craftNamePlaceholder == null && craftNameInput != null)
            craftNamePlaceholder = craftNameInput.placeholder as TMP_Text;
    }

    private void SetSuggestedName(string name)
    {
        suggestedName = name;
        if (craftNamePlaceholder != null)
        {
            craftNamePlaceholder.text = string.IsNullOrEmpty(name)
                ? "Введите уникальное название"
                : name;
        }
    }

    private string GetNameForSave(out string error)
    {
        string enteredName = craftNameInput.text;
        if (!string.IsNullOrWhiteSpace(enteredName))
        {
            error = string.Empty;
            return enteredName;
        }

        if (string.IsNullOrEmpty(suggestedName)
            || saveManager == null
            || !saveManager.TryValidateNewShipName(suggestedName, out _, out _))
        {
            SetSuggestedName(CreateAvailableRandomName());
        }

        if (!string.IsNullOrEmpty(suggestedName))
        {
            error = string.Empty;
            return suggestedName;
        }

        error = "Не удалось подобрать уникальное название крафта.";
        return string.Empty;
    }

    private string CreateAvailableRandomName()
    {
        ResolveProjectDependencies();

        if (saveManager == null)
            return string.Empty;

        int combinationCount = NameAdjectives.Length * NameNouns.Length;
        int startIndex = Random.Range(0, combinationCount);
        for (int offset = 0; offset < combinationCount; offset++)
        {
            int combinationIndex = (startIndex + offset) % combinationCount;
            string candidate = NameAdjectives[combinationIndex / NameNouns.Length]
                + " "
                + NameNouns[combinationIndex % NameNouns.Length];

            if (saveManager.TryValidateNewShipName(candidate, out _, out _))
                return candidate;
        }

        return string.Empty;
    }

    private void ResolveProjectDependencies()
    {
        if (saveManager != null && teamSelectionService != null)
            return;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (saveManager == null && container.HasBinding<SaveManager>())
            saveManager = container.Resolve<SaveManager>();

        if (teamSelectionService == null
            && container.HasBinding<TeamSelectionService>())
        {
            teamSelectionService = container.Resolve<TeamSelectionService>();
        }
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        string textValue,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = textValue;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void StretchWithPadding(RectTransform rectTransform, float horizontal, float vertical)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(horizontal, vertical);
        rectTransform.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private static void CreateDialogButton(
        string objectName,
        RectTransform parent,
        string label,
        Vector2 anchoredPosition,
        Color color,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(164f, 48f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        TextMeshProUGUI text = CreateText(
            "Text",
            buttonRect,
            label,
            22f,
            Vector2.zero,
            Vector2.zero,
            Color.white);
        StretchWithPadding(text.rectTransform, 8f, 4f);
    }

    private static Button CreateRandomNameButton(RectTransform parent, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(
            "Random Name",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(58f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.28f, 0.47f, 0.61f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText(
            "Text",
            buttonRect,
            "RND",
            16f,
            Vector2.zero,
            Vector2.zero,
            Color.white);
        StretchWithPadding(text.rectTransform, 4f, 4f);
        return button;
    }

    private void SetFeedback(string message, bool isError)
    {
        if (feedbackText == null)
            return;

        feedbackText.text = message;
        feedbackText.color = isError
            ? new Color(1f, 0.55f, 0.5f, 1f)
            : new Color(0.65f, 0.9f, 0.95f, 1f);
    }
}
