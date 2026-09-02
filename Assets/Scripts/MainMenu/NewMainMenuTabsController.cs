using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class NewMainMenuTabsController : MonoBehaviour
{
    private enum WindowType
    {
        Tab,
        CraftMenu,
        AdditionalWindow
    }

    private struct NavigationEntry
    {
        public WindowType Type;
        public int TabIndex;
        public GameObject AdditionalWindow;

        public NavigationEntry(
            WindowType type,
            int tabIndex = -1,
            GameObject additionalWindow = null)
        {
            Type = type;
            TabIndex = tabIndex;
            AdditionalWindow = additionalWindow;
        }
    }

    [Serializable]
    private sealed class TabBinding
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject window;

        public Button Button => button;
        public GameObject Window => window;
    }

    [SerializeField] private TabBinding[] tabs = Array.Empty<TabBinding>();
    [SerializeField] private GameObject craftMenu;
    [SerializeField] private Button goToPreviousWindowButton;
    [SerializeField, Min(0)] private int initialTabIndex;
    [SerializeField, Min(0)] private int mapTabIndex = 1;
    [SerializeField] private Color inactiveButtonColor = Color.white;
    [SerializeField] private Color activeButtonColor = Color.blue;

    private readonly List<NavigationEntry> windowHistory = new();
    private int activeTabIndex = -1;
    private int tabIndexBeforeCraft = -1;
    private int tabIndexBeforeAdditionalWindow = -1;
    private bool isCraftMenuOpen;
    private GameObject activeAdditionalWindow;
    private UnityAction[] buttonHandlers;
    private UnityAction previousWindowButtonHandler;

    public int ActiveTabIndex => activeTabIndex;
    public bool CanGoToPreviousWindow => windowHistory.Count > 0;

    private void Start()
    {
        if (tabs == null || tabs.Length == 0)
        {
            Debug.LogError("Configure at least one menu tab.", this);
            return;
        }

        RegisterButtons();
        RegisterPreviousWindowButton();
        int tabToOpen = LevelLoader.ConsumeMapOnMainMenuLoadRequest()
            ? mapTabIndex
            : initialTabIndex;
        NavigateToTab(tabToOpen, false);
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnregisterPreviousWindowButton();
    }

    // Single navigation entry point: opens a window and synchronizes its bottom-tab state.
    public void NavigateToTab(int tabIndex)
    {
        NavigateToTab(tabIndex, true);
    }

    private void NavigateToTab(int tabIndex, bool recordHistory)
    {
        if (tabIndex < 0 || tabIndex >= tabs.Length)
        {
            Debug.LogError(
                $"Tab index {tabIndex} is outside the configured range.",
                this);
            return;
        }

        TabBinding selectedTab = tabs[tabIndex];
        if (selectedTab == null || selectedTab.Window == null)
        {
            Debug.LogError(
                $"Tab {tabIndex} does not have a window assigned.",
                this);
            return;
        }

        if (!isCraftMenuOpen
            && activeAdditionalWindow == null
            && activeTabIndex == tabIndex)
            return;

        if (recordHistory)
            RecordCurrentWindow();

        for (int i = 0; i < tabs.Length; i++)
        {
            TabBinding tab = tabs[i];
            if (tab == null || tab.Window == null)
                continue;

            tab.Window.SetActive(i == tabIndex);
            SetButtonColor(tab.Button, i == tabIndex ? activeButtonColor : inactiveButtonColor);
        }

        SetCraftMenuActive(false);
        isCraftMenuOpen = false;
        SetAdditionalWindowActive(false);
        activeAdditionalWindow = null;
        activeTabIndex = tabIndex;
        UpdatePreviousWindowButton();
    }

    // Opens an extra screen over the active tab and keeps it in the same back-stack.
    public void OpenAdditionalWindow(GameObject window)
    {
        OpenAdditionalWindow(window, true, activeTabIndex);
    }

    private void OpenAdditionalWindow(
        GameObject window,
        bool recordHistory,
        int sourceTabIndex)
    {
        if (window == null)
        {
            Debug.LogError("Additional window is not assigned.", this);
            return;
        }

        if (activeAdditionalWindow == window)
        {
            if (!window.activeInHierarchy)
            {
                if (sourceTabIndex >= 0 && sourceTabIndex < tabs.Length)
                    ActivateTab(sourceTabIndex);

                window.SetActive(true);
            }

            return;
        }

        if (recordHistory)
            RecordCurrentWindow();

        if (sourceTabIndex >= 0 && sourceTabIndex < tabs.Length)
        {
            tabIndexBeforeAdditionalWindow = sourceTabIndex;
            ActivateTab(sourceTabIndex);
        }

        SetCraftMenuActive(false);
        isCraftMenuOpen = false;
        SetAdditionalWindowActive(false);
        activeAdditionalWindow = window;
        activeAdditionalWindow.SetActive(true);
        UpdatePreviousWindowButton();
    }

    // Opens the craft flow as an additional menu screen outside the bottom-tab list.
    public void OpenCraftMenu()
    {
        OpenCraftMenu(true);
    }

    private void OpenCraftMenu(bool recordHistory)
    {
        if (craftMenu == null)
        {
            Debug.LogError("Craft menu is not assigned.", this);
            return;
        }

        if (isCraftMenuOpen)
            return;

        if (recordHistory)
            RecordCurrentWindow();

        if (activeTabIndex >= 0)
            tabIndexBeforeCraft = activeTabIndex;

        for (int i = 0; i < tabs.Length; i++)
        {
            TabBinding tab = tabs[i];
            if (tab == null)
                continue;

            if (tab.Window != null)
                tab.Window.SetActive(false);

            SetButtonColor(tab.Button, inactiveButtonColor);
        }

        SetCraftMenuActive(true);
        isCraftMenuOpen = true;
        SetAdditionalWindowActive(false);
        activeAdditionalWindow = null;
        activeTabIndex = -1;
        UpdatePreviousWindowButton();
    }

    public void CloseCraftMenu()
    {
        if (TryGoToPreviousWindow())
            return;

        int returnTabIndex = tabIndexBeforeCraft;
        if (returnTabIndex < 0 || returnTabIndex >= tabs.Length)
            returnTabIndex = initialTabIndex;

        NavigateToTab(returnTabIndex, false);
    }

    public void GoToPreviousWindow()
    {
        TryGoToPreviousWindow();
    }

    public void GoToPrewWindow()
    {
        GoToPreviousWindow();
    }

    private void RegisterButtons()
    {
        if (buttonHandlers != null)
            return;

        buttonHandlers = new UnityAction[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            TabBinding tab = tabs[i];
            if (tab == null || tab.Button == null)
            {
                Debug.LogError($"Tab {i} does not have a button assigned.", this);
                continue;
            }

            int tabIndex = i;
            UnityAction handler = () => NavigateToTab(tabIndex);
            buttonHandlers[i] = handler;
            tab.Button.onClick.AddListener(handler);
        }
    }

    private void UnregisterButtons()
    {
        if (tabs == null)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            TabBinding tab = tabs[i];
            if (tab == null || tab.Button == null || buttonHandlers == null)
                continue;

            UnityAction handler = buttonHandlers[i];
            if (handler != null)
                tab.Button.onClick.RemoveListener(handler);
        }

        buttonHandlers = null;
    }

    private void RegisterPreviousWindowButton()
    {
        if (goToPreviousWindowButton == null || previousWindowButtonHandler != null)
            return;

        previousWindowButtonHandler = GoToPreviousWindow;
        goToPreviousWindowButton.onClick.AddListener(previousWindowButtonHandler);
        UpdatePreviousWindowButton();
    }

    private void UnregisterPreviousWindowButton()
    {
        if (goToPreviousWindowButton == null || previousWindowButtonHandler == null)
            return;

        goToPreviousWindowButton.onClick.RemoveListener(previousWindowButtonHandler);
        previousWindowButtonHandler = null;
    }

    private void RecordCurrentWindow()
    {
        NavigationEntry entry;
        if (activeAdditionalWindow != null)
        {
            entry = new NavigationEntry(
                WindowType.AdditionalWindow,
                tabIndexBeforeAdditionalWindow,
                activeAdditionalWindow);
        }
        else if (isCraftMenuOpen)
        {
            entry = new NavigationEntry(WindowType.CraftMenu);
        }
        else if (activeTabIndex >= 0)
        {
            entry = new NavigationEntry(WindowType.Tab, activeTabIndex);
        }
        else
        {
            return;
        }

        if (windowHistory.Count > 0 && IsSameWindow(windowHistory[^1], entry))
            return;

        windowHistory.Add(entry);
    }

    private bool TryGoToPreviousWindow()
    {
        if (windowHistory.Count == 0)
        {
            UpdatePreviousWindowButton();
            return false;
        }

        int lastIndex = windowHistory.Count - 1;
        NavigationEntry entry = windowHistory[lastIndex];
        windowHistory.RemoveAt(lastIndex);

        if (entry.Type == WindowType.CraftMenu)
            OpenCraftMenu(false);
        else if (entry.Type == WindowType.AdditionalWindow)
            OpenAdditionalWindow(entry.AdditionalWindow, false, entry.TabIndex);
        else
            NavigateToTab(entry.TabIndex, false);

        UpdatePreviousWindowButton();
        return true;
    }

    private void UpdatePreviousWindowButton()
    {
        if (goToPreviousWindowButton != null)
            goToPreviousWindowButton.interactable = CanGoToPreviousWindow;
    }

    private static bool IsSameWindow(NavigationEntry left, NavigationEntry right)
    {
        return left.Type == right.Type
            && left.TabIndex == right.TabIndex
            && left.AdditionalWindow == right.AdditionalWindow;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.image != null)
            button.image.color = color;
    }

    private void SetCraftMenuActive(bool isActive)
    {
        if (craftMenu != null)
            craftMenu.SetActive(isActive);
    }

    private void SetAdditionalWindowActive(bool isActive)
    {
        if (activeAdditionalWindow != null)
            activeAdditionalWindow.SetActive(isActive);
    }

    private void ActivateTab(int tabIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            TabBinding tab = tabs[i];
            if (tab == null || tab.Window == null)
                continue;

            tab.Window.SetActive(i == tabIndex);
            SetButtonColor(tab.Button, i == tabIndex
                ? activeButtonColor
                : inactiveButtonColor);
        }

        activeTabIndex = tabIndex;
    }

    // Keeps existing Inspector bindings working while callers move to NavigateToTab.
    public void ShowTab(int tabIndex)
    {
        NavigateToTab(tabIndex);
    }
}
