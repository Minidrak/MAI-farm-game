using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FarmSimulator.Data;
using FarmSimulator.Visual;

namespace FarmSimulator.Core
{
    public enum PlantState { Growing, Mature, Harvested, Dead }

    public class PlantInstance : MonoBehaviour
    {
        private const float MinVisualScale = 0.01f;

        // ── Data ─────────────────────────────────────────────────────────────
        public PlantData Data { get; private set; }

        // ── State ────────────────────────────────────────────────────────────
        public PlantState State        { get; private set; } = PlantState.Growing;
        public float      Growth       { get; private set; } = 0f;   // 0..1
        public float      GrowthRate   { get; private set; }
        public float      YieldAmount  { get; private set; }
        public float      MutationChance { get; private set; }
        public float      ValueModifier { get; set; } = 0f;
        public float      BurningValueFactor => _burningValueFactor;
        public bool       HasBurningMutation => _burningMutation != null;

        public IReadOnlyList<MutationData> Mutations => _mutations;

        // External modifiers applied by events / neighbors (reset each tick)
        public float GrowthRateModifier    { get; set; } = 0f;
        public float YieldModifier         { get; set; } = 0f;
        public float MutationChanceModifier{ get; set; } = 0f;

        // ── Private ───────────────────────────────────────────────────────────
        private readonly List<MutationData> _mutations = new();
        private BalanceConfig _cfg;
        private PlantMutationVisuals _mutationVisuals;
        private Vector3 _fullScale = Vector3.one;
        private Vector3 _fullScaleLocalPosition;
        private float _baseYOffsetFromPivot;
        private MutationData _burningMutation;
        private float _burningValueFactor = 1f;

        // ── Init ─────────────────────────────────────────────────────────────
        public void Initialize(PlantData data, BalanceConfig config)
        {
            Data = data;
            _cfg = config;
            _mutations.Clear();
            CacheFullScale();
            GrowthRate = data.baseGrowthRate;
            YieldAmount = data.baseYield;
            MutationChance = data.baseMutationChance;
            State = PlantState.Growing;
            Growth = 0f;
            ValueModifier = 0f;
            _burningMutation = null;
            _burningValueFactor = 1f;
            RecalculateYield(0.5f);
            ApplyVisualGrowth();
            SyncMutationVisuals();
        }

        public void Restore(PlantData data, BalanceConfig config, float growth, PlantState state, IEnumerable<MutationData> mutations, float burningValueFactor = 1f)
        {
            Data = data;
            _cfg = config;
            _mutations.Clear();

            if (mutations != null)
            {
                foreach (MutationData mutation in mutations)
                {
                    if (mutation != null)
                    {
                        _mutations.Add(mutation);
                    }
                }
            }

            State = state;
            Growth = Mathf.Clamp01(growth);
            ValueModifier = 0f;
            _burningValueFactor = Mathf.Clamp(burningValueFactor, 0.01f, 1f);
            CacheFullScale();
            RecalculateStats();
            RecalculateYield(0.5f);
            ApplyVisualGrowth();
            SyncMutationVisuals();
        }

        // ── Growth tick ───────────────────────────────────────────────────────
        /// <summary>Called every growthTickInterval from GridCell.</summary>
        public void Grow(float soilFertility, float soilMoisture, float tickDuration)
        {
            if (State != PlantState.Growing || _cfg == null || Data == null) return;

            float moistureBonus    = soilMoisture    * _cfg.moistureGrowthInfluence;
            float fertilityBonus   = soilFertility   * _cfg.fertilityYieldInfluence;

            float effectiveRate = (GrowthRate + GrowthRateModifier)
                                  * (1f + moistureBonus)
                                  * _cfg.globalGrowthMultiplier;

            effectiveRate = Mathf.Max(0.01f, effectiveRate);

            Growth = Mathf.Clamp01(Growth + effectiveRate * tickDuration);

            RecalculateYield(soilFertility);
            ApplyVisualGrowth();

            if (Growth >= 1f)
            {
                State = PlantState.Mature;
                ApplyVisualGrowth();
            }
        }

        // ── Mutation ──────────────────────────────────────────────────────────
        public bool TryMutate()
        {
            if (_cfg == null) return false;
            return _mutations.Count < _cfg.maxMutationsPerPlant;
        }

        public void ApplyMutation(MutationData mutation)
        {
            if (mutation == null || _cfg == null) return;

            foreach (MutationData existing in _mutations)
            {
                if (existing == mutation) return;
            }

            if (_mutations.Count >= _cfg.maxMutationsPerPlant) return;

            _mutations.Add(mutation);
            RecalculateStats();
            RecalculateYield(0.5f);
            SyncMutationVisuals();

            OnMutationApplied?.Invoke(mutation);
        }

        public void RecalculateStats()
        {
            GrowthRate     = Data.baseGrowthRate;
            MutationChance = Data.baseMutationChance;
            _burningMutation = null;

            foreach (var m in _mutations)
            {
                GrowthRate     *= m.growthRateMultiplier;
                MutationChance += m.mutationChanceDelta;
                if (m != null && m.burnsOverTime)
                {
                    _burningMutation = m;
                }
            }

            MutationChance = Mathf.Clamp01(MutationChance);
        }

        // ── Yield & Value ─────────────────────────────────────────────────────
        public float CalculateYield()
        {
            if (State != PlantState.Mature) return 0f;
            return YieldAmount;
        }

        public float CalculateValue()
        {
            float baseVal = Data.baseValue;
            float valueMultiplier = CalculateMutationValueMultiplier();
            float neighborValueFactor = Mathf.Max(0.1f, 1f + ValueModifier);
            return baseVal * valueMultiplier * neighborValueFactor * _burningValueFactor * CalculateYield();
        }

        public float CalculateMutationValueMultiplier()
        {
            if (_cfg == null)
            {
                return 1f;
            }

            float valueMultiplier = 1f;
            for (int i = 0; i < _mutations.Count; i++)
            {
                MutationData mutation = _mutations[i];
                if (mutation == null)
                {
                    continue;
                }

                valueMultiplier *= GetMutationValueFactor(mutation);
            }

            return valueMultiplier;
        }

        public float GetMutationValueFactor(MutationData mutation)
        {
            if (mutation == null || _cfg == null)
            {
                return 1f;
            }

            float rarityFactor = mutation.rarity switch
            {
                MutationRarity.Common => _cfg.commonMutationValueMult,
                MutationRarity.Rare   => _cfg.rareMutationValueMult,
                MutationRarity.Unique => _cfg.uniqueMutationValueMult,
                _ => 1f
            };

            return mutation.valueMultiplier * rarityFactor;
        }

        public string BuildValueBreakdown()
        {
            if (Data == null)
            {
                return "Нет данных";
            }

            StringBuilder builder = new();
            float yield = CalculateYield();
            float mutationMultiplier = CalculateMutationValueMultiplier();
            builder.Append($"База {Data.baseValue:F0}g x урожай {yield:F1}");

            if (_mutations.Count == 0)
            {
                builder.Append(" x мутации 1.00");
                return builder.ToString();
            }

            builder.Append($" x мутации {mutationMultiplier:F2}");
            builder.Append($" x соседство {Mathf.Max(0.1f, 1f + ValueModifier):F2}");
            builder.Append($" x горение {_burningValueFactor:F2}");

            for (int i = 0; i < _mutations.Count; i++)
            {
                MutationData mutation = _mutations[i];
                if (mutation == null)
                {
                    continue;
                }

                builder.AppendLine();
                builder.Append($"- {mutation.mutationName}: x{GetMutationValueFactor(mutation):F2}");
            }

            return builder.ToString();
        }

        // ── Harvest ───────────────────────────────────────────────────────────
        public float Harvest()
        {
            if (State != PlantState.Mature) return 0f;
            float value = CalculateValue();
            State = PlantState.Harvested;
            ApplyVisualGrowth();
            OnHarvested?.Invoke(value);
            return value;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void RecalculateYield(float soilFertility)
        {
            if (Data == null || _cfg == null)
            {
                YieldAmount = 0f;
                return;
            }

            float fertilityBonus = soilFertility * _cfg.fertilityYieldInfluence;
            float yieldMultiplier = 1f;

            foreach (MutationData mutation in _mutations)
            {
                yieldMultiplier *= mutation.yieldMultiplier;
            }

            YieldAmount = Data.baseYield
                * (1f + fertilityBonus + YieldModifier)
                * yieldMultiplier;
        }

        public float GetCurrentMutationChance()
        {
            return Mathf.Clamp01(MutationChance + MutationChanceModifier);
        }

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<MutationData> OnMutationApplied;
        public event System.Action<float>        OnHarvested;

        private void Update()
        {
            TickBurning(Time.deltaTime);
        }

        private void CacheFullScale()
        {
            _fullScale = transform.localScale;
            if (_fullScale == Vector3.zero)
            {
                _fullScale = Vector3.one;
            }

            _fullScaleLocalPosition = transform.localPosition;
            _baseYOffsetFromPivot = 0f;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                float offset = renderer.bounds.min.y - transform.position.y;
                if (offset < _baseYOffsetFromPivot)
                {
                    _baseYOffsetFromPivot = offset;
                }
            }
        }

        private void ApplyVisualGrowth()
        {
            if (State == PlantState.Harvested || State == PlantState.Dead)
            {
                transform.localScale = Vector3.zero;
                transform.localPosition = _fullScaleLocalPosition;
                return;
            }

            float visualScale = Mathf.Lerp(MinVisualScale, 1f, Mathf.Clamp01(Growth));
            transform.localScale = Vector3.Scale(_fullScale, new Vector3(visualScale, visualScale, visualScale));
            Vector3 localPosition = _fullScaleLocalPosition;
            localPosition.y += _baseYOffsetFromPivot * (1f - visualScale);
            transform.localPosition = localPosition;
        }

        private void SyncMutationVisuals()
        {
            if (_mutationVisuals == null)
            {
                _mutationVisuals = GetComponent<PlantMutationVisuals>();
                if (_mutationVisuals == null)
                {
                    _mutationVisuals = gameObject.AddComponent<PlantMutationVisuals>();
                }
            }

            _mutationVisuals.Sync(_mutations);
        }

        private void TickBurning(float deltaTime)
        {
            if (_burningMutation == null || State == PlantState.Harvested || State == PlantState.Dead || deltaTime <= 0f)
            {
                return;
            }

            float lossPerSecond = Mathf.Clamp01(_burningMutation.burnLossPerSecond);
            float minimumValueFactor = Mathf.Clamp(_burningMutation.minimumValueFactor, 0.01f, 1f);
            _burningValueFactor = Mathf.Max(minimumValueFactor, _burningValueFactor * Mathf.Pow(1f - lossPerSecond, deltaTime));
        }
    }
}
