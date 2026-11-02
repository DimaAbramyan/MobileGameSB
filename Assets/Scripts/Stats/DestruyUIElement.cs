using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DestroyUIElement : MonoBehaviour
{
    [Zenject.Inject(Optional = true)] private SaveManager saveManager;
    [SerializeField] private GameObject toDestroy;

    public void ToDestroy()
    {
        if (toDestroy.GetComponent<Image>().color != Color.white)
        {
            toDestroy.GetComponent<SaveButtonView>()?.ClearTargetSlot();
        }
        string I = toDestroy.GetComponentInChildren<TMP_Text>().text;
        saveManager ??= new SaveManager();
        if (!saveManager.TryDeleteShip(I, out string error))
            Debug.LogWarning(error);
        Destroy(toDestroy);

    }

}
