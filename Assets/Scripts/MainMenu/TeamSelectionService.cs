using System;
using UnityEngine;

// Keeps the two ships selected for the Studio. The legacy class name preserves
// existing Zenject and scene bindings while the menu no longer has teams.
public sealed class TeamSelectionService
{
    public const int SelectedShipCount = 2;

    private const string StorageKey = "MainMenuSelectedShips";

    private readonly SaveManager saveManager;
    private SelectedShipsState state;

    public event Action Changed;

    public TeamSelectionService(SaveManager saveManager)
    {
        this.saveManager = saveManager;
        state = LoadState();
    }

    public SaveShip GetSelectedShip(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return null;

        SaveShip storedShip = state.ships[slotIndex];
        if (storedShip != null)
            return storedShip;

        string shipName = state.shipNames[slotIndex];
        if (string.IsNullOrEmpty(shipName))
            return null;

        saveManager.LoadAllSaves();
        for (int i = 0; i < saveManager.SavedShips.Count; i++)
        {
            SaveShip ship = saveManager.SavedShips[i];
            if (ship != null && ship.shipName == shipName)
            {
                state.ships[slotIndex] = CloneShip(ship);
                SaveState();
                return ship;
            }
        }

        return null;
    }

    public void AssignShipToSlot(int slotIndex, SaveShip ship)
    {
        if (!IsValidSlotIndex(slotIndex) || ship == null || string.IsNullOrEmpty(ship.shipName))
            return;

        state.ships[slotIndex] = CloneShip(ship);
        state.shipNames[slotIndex] = ship.shipName;
        SaveState();
        Changed?.Invoke();
    }

    public void UpdateSelectedShip(SaveShip ship)
    {
        if (ship == null || string.IsNullOrEmpty(ship.shipName))
            return;

        bool updated = false;
        for (int i = 0; i < state.ships.Length; i++)
        {
            if (state.ships[i] == null || state.ships[i].shipName != ship.shipName)
                continue;

            state.ships[i] = CloneShip(ship);
            updated = true;
        }

        if (!updated)
            return;

        SaveState();
        Changed?.Invoke();
    }

    private static SelectedShipsState LoadState()
    {
        string json = PlayerPrefs.GetString(StorageKey, string.Empty);
        SelectedShipsState loadedState = string.IsNullOrEmpty(json)
            ? null
            : JsonUtility.FromJson<SelectedShipsState>(json);

        if (loadedState == null)
            loadedState = new SelectedShipsState();

        loadedState.Validate();
        return loadedState;
    }

    private void SaveState()
    {
        state.Validate();
        PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SelectedShipCount;
    }

    private static SaveShip CloneShip(SaveShip ship)
    {
        return JsonUtility.FromJson<SaveShip>(JsonUtility.ToJson(ship));
    }

    [Serializable]
    private sealed class SelectedShipsState
    {
        public SaveShip[] ships = new SaveShip[SelectedShipCount];
        public string[] shipNames = new string[SelectedShipCount];

        public void Validate()
        {
            if (shipNames == null || shipNames.Length != SelectedShipCount)
                shipNames = new string[SelectedShipCount];

            if (ships == null || ships.Length != SelectedShipCount)
                ships = new SaveShip[SelectedShipCount];
        }
    }
}
