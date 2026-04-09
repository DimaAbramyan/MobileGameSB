using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Linq;
using UnityEngine.UI;
using System;
using Zenject;
using Unity.VectorGraphics;
public class PlayerLevelVisualController : MonoBehaviour
{
    [Inject] ShipSelect shipSelect;
    List<PlayerLevelVisual> levelVisuals;
    List<ParentShip> ships;
    [SerializeField] Sprite HaveLevel;
    [SerializeField] Sprite DontHaveLevel; 
    public void Awake()
    {
        levelVisuals = GetComponentsInChildren<PlayerLevelVisual>().ToList();
        Debug.Log(levelVisuals.Count);
    }
    public void Start()
    {
        ships = shipSelect.GetComponentsInChildren<ParentShip>().ToList();
        Debug.Log(ships.Count);
        foreach (ParentShip ship in ships)
        {
            ship.OnLevelChanged += UpdateUI;
        }
    }
    private void OnEnable()
    {
        shipSelect.OnShipChanged += UpdateUI;
        
    }

    private void OnDisable()
    {
        shipSelect.OnShipChanged -= UpdateUI; 
        foreach (ParentShip ship in ships)
        {
            ship.OnLevelChanged -= UpdateUI;
        }
    }
    public void UpdateUI(int level)
    {
        Image currentImage;
        foreach (PlayerLevelVisual item in levelVisuals)
        {
            currentImage = item.GetComponent<Image>();
            currentImage.sprite = DontHaveLevel;
        }
        int i = 0;
        foreach (PlayerLevelVisual item in levelVisuals)
        {
            currentImage = item.GetComponent<Image>();
            if (i < level)
            {
                currentImage.sprite = HaveLevel;
            }
            else
            {
                currentImage.sprite = DontHaveLevel;
            }
            i++;
        }
    }

}
