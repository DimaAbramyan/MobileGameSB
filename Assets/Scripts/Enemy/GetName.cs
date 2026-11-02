using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class GetName : MonoBehaviour
{
    [Zenject.Inject(Optional = true)] private SaveManager saveManager;
    TMPro.TextMeshPro m_TextMeshPro;
    [SerializeField]
    private GameObject toDestroy;
    public void ToDestroy()
    {
        string I = toDestroy.GetComponentInChildren<TMP_Text>().text;
        saveManager ??= new SaveManager();
        if (!saveManager.TryDeleteShip(I, out string error))
            Debug.LogWarning(error);
        Destroy(toDestroy);
    }
    public void Awake()
    {
        transform.position = new Vector3(150, 150, 0);
        string I = toDestroy.GetComponentInChildren<TMP_Text>().text;
       // m_TextMeshPro.text = I;
    }
}
