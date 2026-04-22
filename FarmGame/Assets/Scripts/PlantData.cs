using UnityEngine;

namespace FarmSimulator.Data
{
    public enum PlantGroup { Vegetables, Fruits, Grains, Exotic }

    [CreateAssetMenu(fileName = "NewPlant", menuName = "FarmSim/Plant Data")]
    public class PlantData : ScriptableObject
    {
        [Header("Identity")]
        public string plantName = "Unknown Plant";
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;
        public GameObject prefab;

        [Header("Group")]
        public PlantGroup group;

        [Header("Stats")]
        [Range(1f, 100f)]  public float baseYield        = 10f;
        [Range(0.1f, 5f)]  public float baseGrowthRate   = 1f;
        [Range(1f, 1000f)] public float baseValue        = 50f;
        [Range(1f, 500f)]  public float seedCost         = 15f;
        public int displayOrder;

        [Header("Mutation")]
        [Range(0f, 1f)] public float baseMutationChance  = 0.05f;

        [Header("Requirements")]
        [Range(0f, 1f)] public float requiredFertility   = 0.3f;
        [Range(0f, 1f)] public float requiredMoisture    = 0.3f;

        [Header("Synergy")]
        public PlantGroup[] synergisticGroups;
        public PlantGroup[] conflictingGroups;
    }
}
