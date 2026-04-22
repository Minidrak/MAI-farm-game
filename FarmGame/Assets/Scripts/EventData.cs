using UnityEngine;

namespace FarmSimulator.Data
{
    public enum EventType { Weather, Biological, Anomalous }
    public enum EventSeverity { Minor, Moderate, Major }

    [System.Serializable]
    public class EventEffect
    {
        [Range(-2f, 2f)] public float growthRateDelta      = 0f;
        [Range(-2f, 2f)] public float yieldDelta           = 0f;
        [Range(-1f, 1f)] public float mutationChanceDelta  = 0f;
        [Range(0f, 1f)]  public float soilFertilityDelta   = 0f;
        [Range(0f, 1f)]  public float soilMoistureDelta    = 0f;
        [Range(0f, 1f)]  public float infectionChance      = 0f;
    }

    [CreateAssetMenu(fileName = "NewEvent", menuName = "FarmSim/Event Data")]
    public class EventData : ScriptableObject
    {
        [Header("Identity")]
        public string eventName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;

        [Header("Type")]
        public EventType   type;
        public EventSeverity severity;

        [Header("Effect")]
        public EventEffect effect;

        [Header("Duration (seconds)")]
        [Range(10f, 300f)] public float duration = 60f;

        [Header("Classification")]
        public bool isPositive = true;

        [Header("Event Mutation")]
        public MutationData eventMutation;
        [Range(0f, 1f)] public float mutationChancePerPlant = 0.03f;
        [Range(5f, 60f)] public float mutationTickInterval = 15f;
        [Range(0f, 1f)] public float destroyPlantChanceOnMutation = 0f;

        [Header("Conditions (triggers only if met)")]
        [Range(0f, 1f)] public float requiredPlantDensity = 0f;
        public PlantGroup[] affectedGroups;
        public bool affectsAllGroups = true;

        [Header("Visual")]
        public GameObject vfxPrefab;
        public Material skyboxMaterial;
        public Color directionalLightColor = Color.white;
        [Range(0f, 8f)] public float directionalLightIntensity = 1f;
        public Color ambientColor = Color.gray;
        public Color fogColor = Color.gray;
        [Range(0f, 0.1f)] public float fogDensity = 0.01f;
        public int visualPriority = 0;
        [Range(0f, 5f)] public float blendInSeconds = 1.8f;
        [Range(0f, 5f)] public float blendOutSeconds = 2f;

        [Header("Spawn weight")]
        [Range(1, 100)] public int weight = 50;
    }
}
