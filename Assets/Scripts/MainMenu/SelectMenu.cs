using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Collections;
using Zenject;
public class SaveLoadUI : MonoBehaviour
{
    [Inject] PrefabFactory prefabFactory;
    public GameObject buttonPrefab;  // Префаб кнопки (сохранения)
    public Transform content;        // Контейнер для кнопок
    public Save LoadTo;              // Куда сохраняется выбранный корабль
    private string savePath;
    private string[] savesThatAlreadyExist;
    void OnEnable()
    {
        savesThatAlreadyExist = FindObjectsOfType<Save>().Select(obj => obj.save.shipName).ToArray();
        //Debug.Log(savesThatAlreadyExist[0] + " - 1 корабль");
        //Debug.Log(savesThatAlreadyExist[1] + " - 2 корабль");
        //Debug.Log(savesThatAlreadyExist[2] + " - 3 корабль");
        savePath = Application.persistentDataPath + "/Saves";
        LoadSaveFiles();
        if (LoadTo.save.shipName != "")
        {
            GameObject newButton = Instantiate(buttonPrefab, content);
            newButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = LoadTo.save.shipName;
            PrintShip(newButton, LoadTo.save);
            newButton.transform.SetAsFirstSibling();
            newButton.GetComponent<Button>().onClick.AddListener(() => CloseAndForget());
            newButton.GetComponent<Image>().color = new Color(0.5f, 0.8f, 1f);
        }
    }
    // Загрузка сохранений в Content
    public void LoadSaveFiles()
    {
        // Очистка старых кнопок
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        // Проверяем, есть ли папка с сохранениями
        if (!Directory.Exists(savePath))
            return;

        // Получаем список файлов сохранений
        string[] files = Directory.GetFiles(savePath, "*.json");

        foreach (string file in files)
        {
            CreateSaveButton(Path.GetFileNameWithoutExtension(file), file);
            TryToFindCreatedShip(file);
        }
    }

    // Создание самой кнопки
    void CreateSaveButton(string saveName, string filePath)
    {
        string json = File.ReadAllText(filePath);
        SaveShip Ship = JsonUtility.FromJson<SaveShip>(json);
        
        if (savesThatAlreadyExist.Contains(Ship.shipName))
            return;
        GameObject newButton = Instantiate(buttonPrefab, content);
        newButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = saveName;
        newButton.GetComponentsInChildren<TMPro.TextMeshProUGUI>()[1].text = Ship.shipDescr;
        newButton.GetComponent<Button>().onClick.AddListener(() => LoadSave(filePath));
        PrintShip(newButton, Ship);
    }

    void LoadSave(string filePath)
    {
        string json = File.ReadAllText(filePath);
        SaveShip Ship = JsonUtility.FromJson<SaveShip>(json);
        LoadTo.save = Ship;
        this.gameObject.SetActive(false);
    }
    void TryToFindCreatedShip(string filePath)
    {
        
    }
    void PrintShip(GameObject newButton, SaveShip Ship)
    {
        Image ImageOfShip = newButton.GetComponentsInChildren<Image>().Skip(1).FirstOrDefault();
        GameObject CreatedShip = Instantiate<GameObject>(prefabFactory.GetShip(Ship.shipId), ImageOfShip.transform);
        foreach (WeaponDataSer weaponsToDraw in Ship.weaponData)
        {
            GameObject weap = Instantiate(prefabFactory.GetWeapon(weaponsToDraw.ID), ImageOfShip.transform);
            weap.transform.localPosition = weaponsToDraw.place/2;
        }
        GameObject[] obj = ImageOfShip.GetComponentsInChildren<Transform>().Skip(1).Select(t => t.gameObject).ToArray();
        
    }
    void CloseAndForget()
    {
        LoadTo.save.shipName = "";
        this.gameObject.SetActive(false);
        LoadTo.GetComponent<Image>().color = Color.white;
    }
}