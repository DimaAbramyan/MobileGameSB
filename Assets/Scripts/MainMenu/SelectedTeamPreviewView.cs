using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class SelectedTeamPreviewView : MonoBehaviour
{
    [Serializable]
    public sealed class ShipRow
    {
        public GameObject root;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text detailsText;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private string title = "Твой отряд";
    [SerializeField] private string emptySlotText = "Слот пуст";
    [SerializeField] private string detailsFormat = "ID: {0} · Оружие: {1}";

    [Header("Rows")]
    [SerializeField] private ShipRow[] rows = Array.Empty<ShipRow>();
    [SerializeField] private bool showEmptyRows = true;

    [Header("Sources")]
    [SerializeField] private bool preferPreparedTeamSave = true;
    [SerializeField] private Save[] saveSlots;

    [InjectOptional] private TeamSave teamSave;
    [InjectOptional] private List<Save> allSaves;
    [InjectOptional] private PrefabFactory prefabFactory;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);

        Refresh();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        if (titleText != null)
            titleText.text = title;

        SaveData[] ships = GetSelectedTeam();

        for (int i = 0; i < rows.Length; i++)
        {
            ShipRow row = rows[i];
            if (row == null)
                continue;

            SaveData ship = i < ships.Length ? ships[i] : null;
            RefreshRow(i, row, ship);
        }
    }

    public void SetSaveSlots(Save[] slots)
    {
        saveSlots = slots;
        Refresh();
    }

    public void SetTeam(SaveData[] team)
    {
        preferPreparedTeamSave = true;
        if (teamSave != null)
            teamSave.AllSavesThatLoaded = team;

        Refresh();
    }

    private SaveData[] GetSelectedTeam()
    {
        if (preferPreparedTeamSave
            && teamSave != null
            && teamSave.AllSavesThatLoaded != null
            && teamSave.AllSavesThatLoaded.Length > 0)
        {
            return teamSave.AllSavesThatLoaded
                .Where(IsFilled)
                .ToArray();
        }

        IEnumerable<Save> source = saveSlots != null && saveSlots.Length > 0
            ? saveSlots
            : allSaves != null
                ? allSaves
                : Enumerable.Empty<Save>();

        return source
            .Where(save => save != null)
            .OrderBy(save => save.SlotIndex)
            .Select(save => save.save)
            .Where(IsFilled)
            .Select(ToSaveData)
            .ToArray();
    }

    private void RefreshRow(int index, ShipRow row, SaveData ship)
    {
        bool hasShip = IsFilled(ship);

        if (row.root != null)
            row.root.SetActive(hasShip || showEmptyRows);

        if (!hasShip)
        {
            if (row.nameText != null)
                row.nameText.text = $"{index + 1}. {emptySlotText}";

            if (row.detailsText != null)
                row.detailsText.text = string.Empty;

            if (row.icon != null)
            {
                row.icon.enabled = false;
                row.icon.sprite = null;
            }

            return;
        }

        if (row.nameText != null)
            row.nameText.text = $"{index + 1}. {ship.shipName}";

        if (row.detailsText != null)
            row.detailsText.text = string.Format(
                detailsFormat,
                ship.shipId,
                CountWeapons(ship.WeaponData));

        if (row.icon != null)
        {
            Sprite sprite = GetShipSprite(ship.shipId);
            row.icon.sprite = sprite;
            row.icon.enabled = sprite != null;
        }
    }

    private Sprite GetShipSprite(int shipId)
    {
        GameObject prefab = prefabFactory?.GetShip(shipId);
        if (prefab == null)
            return null;

        SpriteRenderer renderer =
            prefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    private static int CountWeapons(WeaponDataSer[] weapons)
    {
        return weapons != null
            ? weapons.Count(weapon => weapon != null)
            : 0;
    }

    private static bool IsFilled(SaveShip save)
    {
        return save != null && !string.IsNullOrEmpty(save.shipName);
    }

    private static bool IsFilled(SaveData save)
    {
        return save != null && !string.IsNullOrEmpty(save.shipName);
    }

    private static SaveData ToSaveData(SaveShip save)
    {
        return new SaveData
        {
            shipId = save.shipId,
            shipName = save.shipName,
            WeaponData = save.weaponData
        };
    }
}
