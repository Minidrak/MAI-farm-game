using UnityEngine;

namespace FarmSimulator.Player
{
    public enum WorldInteractionZoneType
    {
        Shop,
        SellStall
    }

    public class WorldInteractionZone : MonoBehaviour
    {
        [SerializeField] private WorldInteractionZoneType _zoneType;
        [SerializeField] private string _displayName = "Точка взаимодействия";
        [TextArea(2, 5)]
        [SerializeField] private string _description = string.Empty;

        public WorldInteractionZoneType ZoneType => _zoneType;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;
        public string Description => _description;
    }
}
