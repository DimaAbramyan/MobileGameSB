using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CreateCraftSaveDialog
{
    public static string Execute()
    {
        CraftSaveController controller = Object.FindFirstObjectByType<CraftSaveController>();
        if (controller == null)
            return "CraftSaveController was not found.";

        SerializedObject serializedController = new(controller);
        SerializedProperty existingDialog = serializedController.FindProperty("saveDialog");
        if (existingDialog.objectReferenceValue != null)
            return "Craft Save Dialog is already assigned.";

        RectTransform host = serializedController.FindProperty("dialogHost")
            .objectReferenceValue as RectTransform;
        if (host == null)
            return "CraftSaveController has no Dialog Host.";

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Craft Save Dialog");

        GameObject dialogRoot = CreateObject(
            "Craft Save Dialog",
            host,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));
        RectTransform rootRect = dialogRoot.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image overlay = dialogRoot.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.58f);

        GameObject panelObject = CreateObject(
            "Panel",
            rootRect,
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(560f, 310f);
        panelObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 1f);

        CreateText(
            "Title",
            panel,
            "Название крафта",
            32f,
            new Vector2(0f, 100f),
            new Vector2(500f, 46f),
            Color.white);

        TMP_InputField nameInput = CreateNameInput(panel);
        TMP_Text feedback = CreateText(
            "Feedback",
            panel,
            string.Empty,
            20f,
            new Vector2(0f, -22f),
            new Vector2(500f, 42f),
            new Color(0.65f, 0.9f, 0.95f, 1f));

        Button saveButton = CreateButton(
            "Save",
            panel,
            "Сохранить",
            new Vector2(-98f, -108f),
            new Color(0.12f, 0.55f, 0.72f, 1f));
        Button cancelButton = CreateButton(
            "Cancel",
            panel,
            "Отмена",
            new Vector2(98f, -108f),
            new Color(0.28f, 0.31f, 0.34f, 1f));
        Button randomNameButton = CreateButton(
            "Random Name",
            panel,
            "Случайно",
            new Vector2(195f, 30f),
            new Color(0.28f, 0.31f, 0.34f, 1f),
            new Vector2(112f, 52f));

        UnityEventTools.AddPersistentListener(saveButton.onClick, controller.SaveCurrentCraft);
        UnityEventTools.AddPersistentListener(cancelButton.onClick, controller.CloseSaveDialog);

        serializedController.Update();
        serializedController.FindProperty("craftNameInput").objectReferenceValue = nameInput;
        serializedController.FindProperty("feedbackText").objectReferenceValue = feedback;
        serializedController.FindProperty("saveDialog").objectReferenceValue = dialogRoot;
        serializedController.FindProperty("randomNameButton").objectReferenceValue = randomNameButton;
        serializedController.FindProperty("createDefaultDialogWhenUnconfigured").boolValue = false;
        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        dialogRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        return "Craft Save Dialog created in scene and disabled.";
    }

    private static GameObject CreateObject(
        string name,
        Transform parent,
        params System.Type[] additionalComponents)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        for (int index = 0; index < additionalComponents.Length; index++)
            gameObject.AddComponent(additionalComponents[index]);

        Undo.RegisterCreatedObjectUndo(gameObject, "Create Craft Save Dialog");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static TMP_InputField CreateNameInput(RectTransform parent)
    {
        GameObject inputObject = CreateObject(
            "Craft Name Input",
            parent,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(TMP_InputField));
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(-30f, 30f);
        inputRect.sizeDelta = new Vector2(388f, 54f);
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
        text.alignment = TextAlignmentOptions.MidlineLeft;

        TextMeshProUGUI placeholder = CreateText(
            "Placeholder",
            inputRect,
            "Введите уникальное название",
            24f,
            Vector2.zero,
            Vector2.zero,
            new Color(0.35f, 0.39f, 0.42f, 1f));
        StretchWithPadding(placeholder.rectTransform, 14f, 8f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = inputRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 48;
        return input;
    }

    private static Button CreateButton(
        string name,
        RectTransform parent,
        string label,
        Vector2 position,
        Color color,
        Vector2? size = null)
    {
        GameObject buttonObject = CreateObject(
            name,
            parent,
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = size ?? new Vector2(160f, 52f);
        buttonObject.GetComponent<Image>().color = color;

        TextMeshProUGUI text = CreateText(
            "Text",
            buttonRect,
            label,
            22f,
            Vector2.zero,
            Vector2.zero,
            Color.white);
        Stretch(text.rectTransform);
        return buttonObject.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateText(
        string name,
        RectTransform parent,
        string value,
        float fontSize,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject textObject = CreateObject(
            name,
            parent,
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void StretchWithPadding(
        RectTransform rectTransform,
        float horizontal,
        float vertical)
    {
        Stretch(rectTransform);
        rectTransform.offsetMin = new Vector2(horizontal, vertical);
        rectTransform.offsetMax = new Vector2(-horizontal, -vertical);
    }
}
