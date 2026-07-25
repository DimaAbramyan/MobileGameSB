using UnityEngine;

public class BodyData : MonoBehaviour
{
   [SerializeField] private ShipSelectionVisualConfig visualConfig;

   public ShipSelectionVisualConfig VisualConfig => visualConfig;
   public int ShipId =>
      visualConfig != null && visualConfig.ShipData != null
         ? visualConfig.ShipData.shipId
         : 0;

   public int shipId => ShipId;
}
