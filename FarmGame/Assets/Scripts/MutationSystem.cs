using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;

namespace FarmSimulator.Core
{
    public class MutationSystem : MonoBehaviour
    {
        [Header("Mutation Pool")]
        [SerializeField] private MutationData[] _allMutations;

        [Header("Config")]
        [SerializeField] private BalanceConfig _config;

        // External event-based modifier (set by FarmEventSystem)
        public float GlobalMutationChanceBonus { get; set; } = 0f;

        // ── Roll ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Rolls whether the plant mutates. If yes, selects and applies a mutation.
        /// Returns the applied MutationData or null.
        /// </summary>
        public MutationData RollMutation(PlantInstance plant, GridCell cell)
        {
            if (plant == null || _allMutations == null || _allMutations.Length == 0)
                return null;

            float chance = Mathf.Clamp01(
                plant.GetCurrentMutationChance()
                + GlobalMutationChanceBonus
                + _config.eventMutationChanceAdd * GlobalMutationChanceBonus
            );

            if (Random.value > chance) return null;

            var mutation = SelectMutation(plant);
            if (mutation == null) return null;

            plant.ApplyMutation(mutation);
            OnMutationTriggered?.Invoke(cell, mutation);
            return mutation;
        }

        // ── Selection: weighted by rarity ─────────────────────────────────────
        private MutationData SelectMutation(PlantInstance plant)
        {
            var pool = BuildPool(plant);
            if (pool.Count == 0) return null;

            int totalWeight = 0;
            foreach (var m in pool) totalWeight += m.spawnWeight;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            foreach (var m in pool)
            {
                cumulative += m.spawnWeight;
                if (roll < cumulative) return m;
            }

            return pool[pool.Count - 1];
        }

        private List<MutationData> BuildPool(PlantInstance plant)
        {
            var pool = new List<MutationData>();

            foreach (var m in _allMutations)
            {
                if (m == null || m.eventOnly)
                {
                    continue;
                }

                bool alreadyApplied = false;
                foreach (var existing in plant.Mutations)
                {
                    if (existing == m)
                    {
                        alreadyApplied = true;
                        break;
                    }
                }

                if (alreadyApplied) continue;
                pool.Add(m);
            }

            return pool;
        }

        // ── Force mutation (for editor / debug) ───────────────────────────────
        public void ForceMutation(PlantInstance plant, GridCell cell, MutationData mutation)
        {
            plant.ApplyMutation(mutation);
            OnMutationTriggered?.Invoke(cell, mutation);
        }

        public event System.Action<GridCell, MutationData> OnMutationTriggered;
    }
}
