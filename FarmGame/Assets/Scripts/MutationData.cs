using UnityEngine;

namespace FarmSimulator.Data
{
    public enum MutationType
    {
        SpeedBoost,
        YieldBoost,
        MutationCatalyst,
        GoldenHarvest,
        NightBloom,
        SpeedPenalty,
        YieldPenalty,
        Unstable
    }

    public enum MutationRarity { Common, Rare, Unique }

    [CreateAssetMenu(fileName = "NewMutation", menuName = "FarmSim/Mutation Data")]
    public class MutationData : ScriptableObject
    {
        [Header("Identity")]
        public string mutationName;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;
        public bool eventOnly;
        public string sourceEventName;

        [Header("Type & Rarity")]
        public MutationType type;
        public MutationRarity rarity;

        [Header("Modifiers (multipliers, 1 = no change)")]
        [Range(0.1f, 5f)] public float growthRateMultiplier    = 1f;
        [Range(0.1f, 5f)] public float yieldMultiplier         = 1f;
        [Range(-1f, 2f)]  public float mutationChanceDelta     = 0f;

        [Header("Value")]
        [Range(1f, 10f)] public float valueMultiplier          = 1f;
        public bool burnsOverTime;
        [Range(0f, 1f)] public float burnLossPerSecond = 0.01f;
        [Range(0.01f, 1f)] public float minimumValueFactor = 0.1f;

        [Header("Visual")]
        public GameObject vfxPrefab;
        public Color particleColor = Color.white;

        [Header("Weights")]
        [Range(1, 100)] public int spawnWeight = 50;

        public bool IsNegative =>
            type == MutationType.SpeedPenalty ||
            type == MutationType.YieldPenalty ||
            type == MutationType.Unstable;
    }
}
