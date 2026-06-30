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
    [SerializeField]
    List<Image> levelVisuals;
    List<ParentShip> ships;
    [SerializeField] Sprite HaveLevel;
    [SerializeField] Sprite DontHaveLevel; 
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
        foreach (Image item in levelVisuals)
        {
            item.sprite = DontHaveLevel;
        }
        int i = 0;
        foreach (Image item in levelVisuals)
        {
            if (i < level)
            {
                item.sprite = HaveLevel;
            }
            i++;
        }
    }

}
