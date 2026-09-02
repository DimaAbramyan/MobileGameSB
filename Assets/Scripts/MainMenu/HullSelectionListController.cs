using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class HullSelectionListController : MonoBehaviour
{
    [SerializeField] private CraftUIButton craftButtonPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    private readonly List<CraftUIButton> createdButtons = new();
    private ContentCatalogService catalogService;
    private ContentProgressService contentProgressService;
    private bool hasStarted;

    [Inject]
    private void Construct(
        ContentCatalogService catalogService,
        ContentProgressService contentProgressService)
    {
        this.catalogService = catalogService;
        this.contentProgressService = contentProgressService;
    }

    private void Start()
    {
        hasStarted = true;
        RegisterProgressService();
        Refresh();
    }

    private void OnEnable()
    {
        if (hasStarted)
            Refresh();
    }

    private void OnDestroy()
    {
        UnregisterProgressService();
        ClearButtons();
    }

    public void Refresh()
    {
        ResolveDependencies();
        if (!ValidateConfiguration())
            return;

        ClearButtons();

        IReadOnlyList<HullContentDefinition> hulls = catalogService.Hulls;
        for (int i = 0; i < hulls.Count; i++)
        {
            HullContentDefinition hull = hulls[i];
            if (hull == null)
                continue;

            bool isOwned = contentProgressService.IsOwned(hull);
            CraftUIButton button = Instantiate(craftButtonPrefab, contentRoot);
            button.SetContent(hull);
            button.SetSelected(craftCreationFlow.SelectedHull == hull);
            button.SetAvailability(isOwned);
            button.SetInteractable(isOwned);
            button.SetClickAction(() => SelectHull(hull, button));
            createdButtons.Add(button);
        }
    }

    private void SelectHull(HullContentDefinition hull, CraftUIButton selectedButton)
    {
        if (!contentProgressService.IsOwned(hull))
            return;

        craftCreationFlow.SetSelectedHull(hull);

        for (int i = 0; i < createdButtons.Count; i++)
        {
            CraftUIButton button = createdButtons[i];
            if (button != null)
                button.SetSelected(button == selectedButton);
        }
    }

    private void ClearButtons()
    {
        for (int i = 0; i < createdButtons.Count; i++)
        {
            CraftUIButton button = createdButtons[i];
            if (button != null)
                Destroy(button.gameObject);
        }

        createdButtons.Clear();
    }

    private void RegisterProgressService()
    {
        if (contentProgressService != null)
            contentProgressService.ProgressChanged += Refresh;
    }

    private void UnregisterProgressService()
    {
        if (contentProgressService != null)
            contentProgressService.ProgressChanged -= Refresh;
    }

    private void ResolveDependencies()
    {
        if (catalogService != null && contentProgressService != null)
            return;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (catalogService == null && container.HasBinding<ContentCatalogService>())
            catalogService = container.Resolve<ContentCatalogService>();

        if (contentProgressService == null
            && container.HasBinding<ContentProgressService>())
        {
            contentProgressService = container.Resolve<ContentProgressService>();
            if (hasStarted)
                RegisterProgressService();
        }
    }

    private bool ValidateConfiguration()
    {
        if (catalogService == null || contentProgressService == null)
        {
            Debug.LogError("Hull selection could not resolve content services.", this);
            return false;
        }

        if (craftButtonPrefab == null || contentRoot == null || craftCreationFlow == null)
        {
            Debug.LogError("Configure the hull selection list references.", this);
            return false;
        }

        return true;
    }
}
