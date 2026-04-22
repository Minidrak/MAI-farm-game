using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;

namespace FarmSimulator.Core
{
    [System.Serializable]
    public class SoilParams
    {
        [Range(0f, 1f)] public float fertility  = 0.5f;
        [Range(0f, 1f)] public float moisture   = 0.5f;
        [Range(0f, 1f)] public float infection  = 0f;
    }

    public class GridCell : MonoBehaviour
    {
        public const float RepairCost = 150f;

        [Header("Coordinates")]
        [SerializeField] private int _plotId;
        [SerializeField] private int _x;
        [SerializeField] private int _y;

        [Header("Visual")]
        [SerializeField] private Transform _plantAnchor;
        // ── Identity ──────────────────────────────────────────────────────────
        public int PlotId => _plotId;
        public int X => _x;
        public int Y => _y;

        // ── Soil ──────────────────────────────────────────────────────────────
        public SoilParams Soil = new();
        public bool RequiresWatering { get; private set; }
        public bool IsDestroyed { get; private set; }

        // ── Plant ─────────────────────────────────────────────────────────────
        public PlantInstance Plant { get; private set; }
        public bool IsEmpty => Plant == null;
        public bool HasPlant => Plant != null;
        public bool IsMature => Plant != null && Plant.State == PlantState.Mature;

        // ── Refs ──────────────────────────────────────────────────────────────
        private BalanceConfig    _cfg;
        private MutationSystem   _mutationSystem;
        private float            _growthTimer;

        // ── Init ──────────────────────────────────────────────────────────────
        public void SetCoordinates(int plotId, int x, int y)
        {
            _plotId = plotId;
            _x = x;
            _y = y;
        }

        public void Initialize(int plotId, int x, int y, BalanceConfig cfg, MutationSystem mutationSystem, bool resetSoil = true)
        {
            SetCoordinates(plotId, x, y);
            BindRuntime(cfg, mutationSystem, resetSoil);
        }

        public void BindRuntime(BalanceConfig cfg, MutationSystem mutationSystem, bool resetSoil = false)
        {
            _cfg = cfg;
            _mutationSystem = mutationSystem;

            if (resetSoil)
            {
                Soil.fertility = cfg.defaultFertility;
                Soil.moisture = cfg.defaultMoisture;
                Soil.infection = 0f;
            }
        }

        // ── Planting ──────────────────────────────────────────────────────────
        public bool TryPlant(PlantData data)
        {
            if (IsDestroyed || !IsEmpty || data == null || _cfg == null) return false;

            Plant = CreatePlantInstance(data);
            Plant.Initialize(data, _cfg);
            SubscribeToPlant(Plant);

            _mutationSystem?.RollMutation(Plant, this);
            OnPlanted?.Invoke(this, Plant);
            return true;
        }

        public bool RestorePlant(PlantData data, IEnumerable<MutationData> mutations, float growth, PlantState state, float burningValueFactor = 1f)
        {
            if (data == null || _cfg == null) return false;

            ClearPlantInternal(notify: false);

            Plant = CreatePlantInstance(data);
            Plant.Restore(data, _cfg, growth, state, mutations, burningValueFactor);
            SubscribeToPlant(Plant);
            return true;
        }

        public PlantInstance RemovePlant()
        {
            return ClearPlantInternal(notify: true);
        }

        // ── Update: growth tick ───────────────────────────────────────────────
        private void Update()
        {
            if (IsDestroyed || RequiresWatering || IsEmpty || Plant.State != PlantState.Growing || _cfg == null) return;

            _growthTimer += Time.deltaTime;
            if (_growthTimer < _cfg.growthTickInterval) return;

            float tickDuration = _growthTimer;
            _growthTimer = 0f;
            Soil.infection = Mathf.Clamp01(Soil.infection - _cfg.infectionDecay * tickDuration);

            Plant.Grow(Soil.fertility, Soil.moisture, tickDuration);

            // Growth-phase mutation roll on each tick
            _mutationSystem?.RollMutation(Plant, this);
        }

        // ── Soil API ──────────────────────────────────────────────────────────
        public void ModifySoil(float fertilityDelta, float moistureDelta, float infectionDelta)
        {
            SetSoil(
                Soil.fertility + fertilityDelta,
                Soil.moisture + moistureDelta,
                Soil.infection + infectionDelta);
        }

        public void SetSoil(float fertility, float moisture, float infection)
        {
            Soil.fertility = Mathf.Clamp01(fertility);
            Soil.moisture = Mathf.Clamp01(moisture);
            Soil.infection = Mathf.Clamp01(infection);
        }

        public void SetRequiresWatering(bool requiresWatering)
        {
            RequiresWatering = requiresWatering;
        }

        public void Water(float moistureAmount = 0.35f)
        {
            RequiresWatering = false;
            ModifySoil(0f, moistureAmount, 0f);
        }

        public void DestroyBed()
        {
            IsDestroyed = true;
            RequiresWatering = false;

            if (!IsEmpty)
            {
                RemovePlant();
            }
        }

        public void RestoreBed()
        {
            IsDestroyed = false;
            RequiresWatering = false;
            Soil.infection = 0f;
            Soil.moisture = Mathf.Max(Soil.moisture, _cfg != null ? _cfg.defaultMoisture : 0.5f);
        }

        private PlantInstance CreatePlantInstance(PlantData data)
        {
            Transform parent = _plantAnchor != null ? _plantAnchor : transform;
            Vector3 spawnPosition = _plantAnchor != null ? _plantAnchor.position : transform.position;

            GameObject go = data.prefab != null
                ? Instantiate(data.prefab, spawnPosition, Quaternion.identity, parent)
                : new GameObject($"Plant_{data.plantName}");

            if (data.prefab == null)
            {
                go.transform.SetParent(parent, false);
                go.transform.position = spawnPosition;
            }

            AlignPlantToAnchor(go.transform, parent.position.y);
            return go.GetComponent<PlantInstance>() ?? go.AddComponent<PlantInstance>();
        }

        private static void AlignPlantToAnchor(Transform plantTransform, float anchorY)
        {
            if (plantTransform == null)
            {
                return;
            }

            Renderer[] renderers = plantTransform.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            float minY = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    minY = renderer.bounds.min.y;
                    hasBounds = true;
                }
                else
                {
                    minY = Mathf.Min(minY, renderer.bounds.min.y);
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 position = plantTransform.position;
            position.y += anchorY - minY;
            plantTransform.position = position;
        }

        private PlantInstance ClearPlantInternal(bool notify)
        {
            if (Plant == null)
            {
                return null;
            }

            PlantInstance removed = Plant;
            UnsubscribeFromPlant(removed);
            Plant = null;
            _growthTimer = 0f;

            if (notify)
            {
                OnPlantRemoved?.Invoke(this);
            }

            removed.transform.SetParent(null, true);
            removed.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(removed.gameObject);
            }
            else
            {
                DestroyImmediate(removed.gameObject);
            }

            return removed;
        }

        private void SubscribeToPlant(PlantInstance plant)
        {
            if (plant == null)
            {
                return;
            }

            plant.OnMutationApplied += HandleMutationApplied;
            plant.OnHarvested += HandleHarvested;
        }

        private void UnsubscribeFromPlant(PlantInstance plant)
        {
            if (plant == null)
            {
                return;
            }

            plant.OnMutationApplied -= HandleMutationApplied;
            plant.OnHarvested -= HandleHarvested;
        }

        // ── Callbacks ─────────────────────────────────────────────────────────
        private void HandleMutationApplied(MutationData m) => OnCellMutated?.Invoke(this, m);
        private void HandleHarvested(float value)          => OnCellHarvested?.Invoke(this, value);

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<GridCell, PlantInstance> OnPlanted;
        public event System.Action<GridCell>                OnPlantRemoved;
        public event System.Action<GridCell, MutationData>  OnCellMutated;
        public event System.Action<GridCell, float>         OnCellHarvested;
    }
}
