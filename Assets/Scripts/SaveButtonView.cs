using UnityEngine;
using System.IO;
using Zenject;
public class SaveButtonView : MonoBehaviour
{
    [Inject] private PanelManager _panelManager;
    Save LoadTo;
    private string _filePath;

    public void Init(string filePath, Save loadTo)
    {
        LoadTo = loadTo;
        _filePath = filePath;
    }

    public void OnClick()
    {
        string json = File.ReadAllText(_filePath);
        SaveShip ship = JsonUtility.FromJson<SaveShip>(json);
        LoadTo.save = ship;
        _panelManager.CloseEveryPanel();
    }
}
