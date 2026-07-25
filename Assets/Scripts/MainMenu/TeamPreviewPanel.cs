using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zenject;

public sealed class TeamPreviewPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text[] shipLines;
    [SerializeField] private Save[] saveSlots;
    [SerializeField] private string title = "Твой отряд";
    [SerializeField] private string emptyLineText = "Слот пуст";

    [InjectOptional] private List<Save> allSaves;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (root != null)
            root.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        Save[] saves = saveSlots != null && saveSlots.Length > 0
            ? saveSlots.OrderBy(save => save != null ? save.SlotIndex : int.MaxValue).ToArray()
            : allSaves != null
            ? allSaves.OrderBy(save => save.SlotIndex).ToArray()
            : GetComponentsInChildren<Save>(true)
                .OrderBy(save => save.SlotIndex)
                .ToArray();

        for (int i = 0; i < shipLines.Length; i++)
        {
            TMP_Text line = shipLines[i];
            if (line == null)
                continue;

            Save save = i < saves.Length ? saves[i] : null;
            line.text = FormatSaveLine(i, save);
        }
    }

    private string FormatSaveLine(int index, Save save)
    {
        if (save == null
            || save.save == null
            || string.IsNullOrEmpty(save.save.shipName))
        {
            return $"{index + 1}. {emptyLineText}";
        }

        int weaponCount = save.save.weaponData != null
            ? save.save.weaponData.Count(item => item != null)
            : 0;

        return $"{index + 1}. {save.save.shipName} "
            + $"(ID: {save.save.shipId}, оружие: {weaponCount})";
    }
}
