using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;
using EventType = FarmSimulator.Data.EventType;

namespace FarmSimulator.Core
{
    public class ActiveEvent
    {
        public EventData Data;
        public float TimeRemaining;
        public float MutationTickTimer;
    }

    public class FarmEventSystem : MonoBehaviour
    {
        [Header("Event Pool")]
        [SerializeField] private EventData[] _allEvents;

        [Header("References")]
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private GridManager _grid;
        [SerializeField] private MutationSystem _mutationSystem;

        private readonly List<ActiveEvent> _activeEvents = new();
        private float _nextEventTimer;

        private void Start()
        {
            ScheduleNextEvent();
        }

        private void Update()
        {
            _nextEventTimer -= Time.deltaTime;
            if (_nextEventTimer <= 0f)
            {
                TriggerEvent();
                ScheduleNextEvent();
            }

            TickActiveEvents();
        }

        public ActiveEvent TriggerEvent()
        {
            EventData candidate = SelectEvent();
            if (candidate == null)
            {
                return null;
            }

            ActiveEvent active = new()
            {
                Data = candidate,
                TimeRemaining = candidate.duration > 0f ? candidate.duration : _config.activeEventDuration,
                MutationTickTimer = GetMutationTickInterval(candidate)
            };

            _activeEvents.Add(active);
            ApplyEventEffects(active);
            OnEventStarted?.Invoke(active);
            return active;
        }

        private void ScheduleNextEvent()
        {
            if (_config != null && _config.scheduledEventInterval > 0f)
            {
                _nextEventTimer = _config.scheduledEventInterval;
                return;
            }

            _nextEventTimer = Random.Range(_config.eventIntervalMin, _config.eventIntervalMax);
        }

        private void TickActiveEvents()
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                ActiveEvent active = _activeEvents[i];
                active.TimeRemaining -= Time.deltaTime;
                active.MutationTickTimer -= Time.deltaTime;

                if (active.MutationTickTimer <= 0f)
                {
                    ProcessEventMutationTick(active);
                    active.MutationTickTimer += GetMutationTickInterval(active.Data);
                }

                if (active.TimeRemaining <= 0f)
                {
                    RemoveEventEffects(active);
                    OnEventEnded?.Invoke(active);
                    _activeEvents.RemoveAt(i);
                }
            }
        }

        public void ApplyEventEffects(ActiveEvent active)
        {
            EventEffect effect = active.Data.effect;
            float density = _grid.GetPlantDensity();

            foreach (GridCell cell in _grid.GetAllCells())
            {
                ModifyCellSoil(cell, effect);

                if (cell.IsEmpty || !AffectsPlant(active.Data, cell.Plant))
                {
                    continue;
                }

                cell.Plant.GrowthRateModifier += effect.growthRateDelta;
                cell.Plant.YieldModifier += effect.yieldDelta;
                cell.Plant.MutationChanceModifier += effect.mutationChanceDelta;

                if (effect.infectionChance > 0f && density >= _config.densityEventThreshold && Random.value < effect.infectionChance * density)
                {
                    cell.ModifySoil(0f, 0f, 0.2f);
                }
            }

            if (active.Data.type == EventType.Anomalous && _mutationSystem != null)
            {
                _mutationSystem.GlobalMutationChanceBonus += effect.mutationChanceDelta;
            }

            ApplyEventGameplayHooks(active.Data);
        }

        private void RemoveEventEffects(ActiveEvent active)
        {
            EventEffect effect = active.Data.effect;

            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell.IsEmpty || !AffectsPlant(active.Data, cell.Plant))
                {
                    continue;
                }

                cell.Plant.GrowthRateModifier -= effect.growthRateDelta;
                cell.Plant.YieldModifier -= effect.yieldDelta;
                cell.Plant.MutationChanceModifier -= effect.mutationChanceDelta;
            }

            if (active.Data.type == EventType.Anomalous && _mutationSystem != null)
            {
                _mutationSystem.GlobalMutationChanceBonus -= effect.mutationChanceDelta;
            }
        }

        private void ProcessEventMutationTick(ActiveEvent active)
        {
            if (active == null || active.Data == null || _grid == null)
            {
                return;
            }

            float mutationChance = active.Data.mutationChancePerPlant > 0f
                ? active.Data.mutationChancePerPlant
                : (_config != null ? _config.eventMutationChancePerPlant : 0.03f);

            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell.IsEmpty || !AffectsPlant(active.Data, cell.Plant))
                {
                    continue;
                }

                if (Random.value > mutationChance)
                {
                    continue;
                }

                if (active.Data.eventMutation != null)
                {
                    _mutationSystem?.ForceMutation(cell.Plant, cell, active.Data.eventMutation);
                }

                if (active.Data.destroyPlantChanceOnMutation > 0f && Random.value <= active.Data.destroyPlantChanceOnMutation)
                {
                    cell.RemovePlant();
                }
            }

            _grid.RecalcAllNeighborBonuses();
        }

        private void ApplyEventGameplayHooks(EventData eventData)
        {
            if (_grid == null || eventData == null)
            {
                return;
            }

            if (IsNamedEvent(eventData, "Засуха"))
            {
                ApplyDroughtHooks();
            }
            else if (IsNamedEvent(eventData, "Ливень"))
            {
                ApplyDownpourHooks();
            }
            else if (IsNamedEvent(eventData, "Полная луна"))
            {
                ApplyFullMoonHooks();
            }
            else if (IsNamedEvent(eventData, "Солнцестояние"))
            {
                ApplySolsticeHooks();
            }
            else if (IsNamedEvent(eventData, "Мороз"))
            {
                ApplyFrostHooks();
            }
            else if (IsNamedEvent(eventData, "Вьюга"))
            {
                ApplyBlizzardHooks();
            }
            else if (IsNamedEvent(eventData, "Гроза"))
            {
                ApplyStormHooks();
            }
        }

        private void ApplyDroughtHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.ModifySoil(0f, -0.18f, 0f);
                if (cell.Soil.moisture <= 0.35f)
                {
                    cell.SetRequiresWatering(true);
                }
            }
        }

        private void ApplyDownpourHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.Water(0.25f);
                cell.ModifySoil(0f, 0.08f, -0.05f);
            }
        }

        private void ApplyFullMoonHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.ModifySoil(0.05f, 0.02f, -0.02f);
                if (!cell.IsEmpty && cell.Plant.State == PlantState.Growing)
                {
                    cell.Plant.Grow(cell.Soil.fertility, cell.Soil.moisture, 6f);
                }
            }
        }

        private void ApplySolsticeHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null)
                {
                    continue;
                }

                if (!cell.IsDestroyed)
                {
                    cell.Water(0.18f);
                    cell.ModifySoil(0.1f, 0f, -0.06f);
                }
            }

            RestoreRandomDestroyedBeds(1);
        }

        private void ApplyFrostHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.ModifySoil(-0.03f, -0.08f, 0.04f);
                if (!cell.IsEmpty && cell.Plant.State == PlantState.Growing && Random.value <= 0.18f)
                {
                    cell.SetRequiresWatering(true);
                }
            }
        }

        private void ApplyBlizzardHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.ModifySoil(-0.02f, -0.12f, 0.05f);
            }

            DestroyRandomBeds(1);
        }

        private void ApplyStormHooks()
        {
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell == null || cell.IsDestroyed)
                {
                    continue;
                }

                cell.ModifySoil(-0.01f, 0f, 0.08f);
            }

            DestroyRandomBeds(2);
        }

        private float GetMutationTickInterval(EventData eventData)
        {
            if (eventData != null && eventData.mutationTickInterval > 0f)
            {
                return eventData.mutationTickInterval;
            }

            return _config != null ? _config.eventMutationTickInterval : 15f;
        }

        private EventData SelectEvent()
        {
            float density = _grid.GetPlantDensity();
            List<(EventData evt, int weight)> pool = new();
            foreach (EventData eventData in _allEvents)
            {
                if (eventData == null || density < eventData.requiredPlantDensity)
                {
                    continue;
                }

                pool.Add((eventData, eventData.weight));
            }

            if (pool.Count == 0)
            {
                return null;
            }

            int totalWeight = 0;
            foreach ((EventData _, int weight) in pool)
            {
                totalWeight += weight;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach ((EventData evt, int weight) in pool)
            {
                cumulative += weight;
                if (roll < cumulative)
                {
                    return evt;
                }
            }

            return pool[pool.Count - 1].evt;
        }

        private void ModifyCellSoil(GridCell cell, EventEffect effect)
        {
            if (effect.soilFertilityDelta != 0f || effect.soilMoistureDelta != 0f)
            {
                cell.ModifySoil(effect.soilFertilityDelta, effect.soilMoistureDelta, 0f);
            }
        }

        private bool AffectsPlant(EventData evt, PlantInstance plant)
        {
            if (evt.affectsAllGroups)
            {
                return true;
            }

            foreach (PlantGroup group in evt.affectedGroups)
            {
                if (plant.Data.group == group)
                {
                    return true;
                }
            }

            return false;
        }

        private void DestroyRandomBeds(int count)
        {
            if (_grid == null || count <= 0)
            {
                return;
            }

            List<GridCell> candidates = new();
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell != null && !cell.IsDestroyed)
                {
                    candidates.Add(cell);
                }
            }

            int remaining = Mathf.Min(count, candidates.Count);
            while (remaining > 0 && candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                GridCell cell = candidates[index];
                candidates.RemoveAt(index);
                cell.DestroyBed();
                remaining--;
            }

            _grid.RecalcAllNeighborBonuses();
        }

        private void RestoreRandomDestroyedBeds(int count)
        {
            if (_grid == null || count <= 0)
            {
                return;
            }

            List<GridCell> destroyed = new();
            foreach (GridCell cell in _grid.GetAllCells())
            {
                if (cell != null && cell.IsDestroyed)
                {
                    destroyed.Add(cell);
                }
            }

            int remaining = Mathf.Min(count, destroyed.Count);
            while (remaining > 0 && destroyed.Count > 0)
            {
                int index = Random.Range(0, destroyed.Count);
                GridCell cell = destroyed[index];
                destroyed.RemoveAt(index);
                cell.RestoreBed();
                remaining--;
            }

            _grid.RecalcAllNeighborBonuses();
        }

        private static bool ShouldDamageBeds(EventData eventData)
        {
            return IsNamedEvent(eventData, "Гроза") || IsNamedEvent(eventData, "Вьюга");
        }

        private static int GetDestroyedBedCount(EventData eventData)
        {
            if (IsNamedEvent(eventData, "Гроза"))
            {
                return 2;
            }

            if (IsNamedEvent(eventData, "Вьюга"))
            {
                return 1;
            }

            return 0;
        }

        private static bool IsNamedEvent(EventData eventData, string expectedName)
        {
            return eventData != null && eventData.eventName == expectedName;
        }

        public event System.Action<ActiveEvent> OnEventStarted;
        public event System.Action<ActiveEvent> OnEventEnded;

        public IReadOnlyList<ActiveEvent> ActiveEvents => _activeEvents;
        public IReadOnlyList<EventData> AllEvents => _allEvents;
        public float NextEventTimer => _nextEventTimer;

        public void RestoreState(IEnumerable<ActiveEvent> activeEvents, float nextEventTimer)
        {
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                RemoveEventEffects(_activeEvents[i]);
            }

            _activeEvents.Clear();
            _nextEventTimer = Mathf.Max(1f, nextEventTimer);

            if (activeEvents == null)
            {
                return;
            }

            foreach (ActiveEvent active in activeEvents)
            {
                if (active?.Data == null)
                {
                    continue;
                }

                active.MutationTickTimer = GetMutationTickInterval(active.Data);
                _activeEvents.Add(active);
                ApplyEventEffects(active);
            }
        }

        public EventData FindEventByName(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return null;
            }

            foreach (EventData eventData in _allEvents)
            {
                if (eventData != null && eventData.eventName == eventName)
                {
                    return eventData;
                }
            }

            return null;
        }
    }
}
