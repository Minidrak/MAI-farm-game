using UnityEngine;

namespace FarmSimulator.Data
{
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "FarmSim/Balance Config")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Grid")]
        public int gridWidth  = 4;
        public int gridHeight = 4;
        public int plotCount  = 2;

        [Header("Growth")]
        [Range(0.1f, 10f)] public float growthTickInterval = 5f;
        [Range(0.001f, 5f)]  public float globalGrowthMultiplier = 0.008f;

        [Header("Neighbor bonuses")]
        [Range(0f, 1f)] public float synergyBonus     = 0.20f;
        [Range(0f, 1f)] public float conflictPenalty  = 0.15f;
        // Non-linear stacking: each additional bonus is reduced by this factor
        [Range(0f, 1f)] public float bonusStackDecay  = 0.75f;
        [Range(4, 8)]   public int   maxNeighborCount = 4;

        [Header("Mutations")]
        [Range(0f, 1f)] public float baseMutationChance      = 0.05f;
        [Range(0f, 1f)] public float growthMutationChanceAdd = 0.02f;
        [Range(0f, 1f)] public float eventMutationChanceAdd  = 0.10f;
        [Range(0f, 1f)] public float maxMutationChance       = 0.80f;
        public int maxMutationsPerPlant = 5;

        [Header("Events")]
        [Range(30f, 600f)] public float eventIntervalMin = 60f;
        [Range(30f, 600f)] public float eventIntervalMax = 180f;
        [Range(0f, 1f)]    public float densityEventThreshold = 0.75f;
        [Range(60f, 900f)] public float scheduledEventInterval = 300f;
        [Range(30f, 300f)] public float activeEventDuration = 120f;
        [Range(5f, 60f)] public float eventMutationTickInterval = 15f;
        [Range(0f, 1f)] public float eventMutationChancePerPlant = 0.03f;

        [Header("Economy")]
        [Range(0.1f, 2f)] public float rareMutationValueMult   = 2.5f;
        [Range(0.1f, 2f)] public float uniqueMutationValueMult = 5.0f;
        [Range(0.1f, 2f)] public float commonMutationValueMult = 1.3f;

        [Header("Soil")]
        [Range(0f, 1f)] public float defaultFertility = 0.5f;
        [Range(0f, 1f)] public float defaultMoisture  = 0.5f;
        [Range(0f, 1f)] public float infectionDecay   = 0.1f;
        // How much fertility/moisture affects yield and growth
        [Range(0f, 2f)] public float fertilityYieldInfluence = 0.5f;
        [Range(0f, 2f)] public float moistureGrowthInfluence = 0.5f;

        [Header("Interaction")]
        [Range(0.5f, 6f)] public float interactionRange = 2.8f;
        [Range(0.1f, 20f)] public float playerMoveSpeed = 4.5f;
        [Range(120f, 1080f)] public float promptScreenOffset = 180f;

        [Header("Inventory")]
        [Range(8, 200)] public int inventorySlotCount = 100;

        [Header("Save")]
        public string saveSlotName = "vertical-slice-save.json";
    }
}
