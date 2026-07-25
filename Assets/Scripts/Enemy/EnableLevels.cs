using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class EnableLevels : MonoBehaviour
{
    [SerializeField] public List<Button> ButtonToEnable;
    [SerializeField] private bool disableButtonsWithoutLevelConfig;

    [InjectOptional] private LevelProgressService progressService;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    void Start()
    {
        for (int i = 0; i < ButtonToEnable.Count; i++)
        {
            Button button = ButtonToEnable[i];
            if (button == null)
                continue;

            LoadLevelConfig loader =
                button.GetComponent<LoadLevelConfig>()
                ?? button.GetComponentInChildren<LoadLevelConfig>(true)
                ?? button.GetComponentInParent<LoadLevelConfig>();

            if (loader == null || loader.LevelConfig == null)
            {
                button.interactable = !disableButtonsWithoutLevelConfig;
                continue;
            }

            button.interactable = Progress.CanStartLevel(loader.LevelConfig);
        }
    }
}
