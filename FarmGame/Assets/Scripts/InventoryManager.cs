using System;
using System.Collections.Generic;
using FarmSimulator.Data;
using UnityEngine;

namespace FarmSimulator.Core
{
    public enum InventoryItemKind
    {
        Seed,
        Crop
    }

    [Serializable]
    public class InventorySlotData
    {
        public InventoryItemKind kind;
        public PlantData plant;
        public int quantity;
        public float saleValue;
        public float baseSaleValue;
        public float minimumSaleValue;
        public float valueMultiplier = 1f;
        public int mutationCount;
        public MutationRarity highestRarity;
        public string mutationSummary;
        public string valueBreakdown;
        public bool isBurning;

        public bool IsEmpty => plant == null || quantity <= 0;
        public string DisplayName => plant == null ? "Пусто" : plant.plantName;
    }

    public class InventoryManager : MonoBehaviour
    {
        [SerializeField] private int _slotCount = 100;

        private readonly List<InventorySlotData> _slots = new();
        private bool _hasWateringCan;
        private float _burningTickTimer;

        public IReadOnlyList<InventorySlotData> Slots => _slots;
        public int SlotCount => _slotCount;
        public bool HasWateringCan => _hasWateringCan;
        public int OccupiedSlotsCount
        {
            get
            {
                int occupied = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].IsEmpty)
                    {
                        occupied++;
                    }
                }

                return occupied;
            }
        }

        private void Awake()
        {
            EnsureSlots();
        }

        private void Update()
        {
            TickBurningSlots(Time.deltaTime);
        }

        public void Configure(int slotCount)
        {
            _slotCount = Mathf.Max(1, slotCount);
            EnsureSlots();
            OnInventoryChanged?.Invoke();
        }

        public int GetSeedCount(PlantData plant)
        {
            return CountItems(InventoryItemKind.Seed, plant);
        }

        public int GetCropCount(PlantData plant)
        {
            return CountItems(InventoryItemKind.Crop, plant);
        }

        public int GetCropBatchCount()
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].kind == InventoryItemKind.Crop)
                {
                    count++;
                }
            }

            return count;
        }

        public bool CanAddSeed(PlantData plant)
        {
            return FindCompatibleSeedSlot(plant) >= 0 || FindEmptySlot() >= 0;
        }

        public bool TryAddSeed(PlantData plant, int quantity = 1)
        {
            if (plant == null || quantity <= 0)
            {
                return false;
            }

            EnsureSlots();
            int slotIndex = FindCompatibleSeedSlot(plant);
            if (slotIndex < 0)
            {
                slotIndex = FindEmptySlot();
            }

            if (slotIndex < 0)
            {
                return false;
            }

            InventorySlotData slot = _slots[slotIndex];
            slot.kind = InventoryItemKind.Seed;
            slot.plant = plant;
            slot.quantity += quantity;
            slot.saleValue = 0f;
            slot.baseSaleValue = 0f;
            slot.minimumSaleValue = 0f;
            slot.valueMultiplier = 1f;
            slot.mutationCount = 0;
            slot.highestRarity = MutationRarity.Common;
            slot.mutationSummary = string.Empty;
            slot.valueBreakdown = string.Empty;
            slot.isBurning = false;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool TryConsumeSeed(PlantData plant, int quantity = 1)
        {
            if (plant == null || quantity <= 0)
            {
                return false;
            }

            EnsureSlots();
            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlotData slot = _slots[i];
                if (slot.IsEmpty || slot.kind != InventoryItemKind.Seed || slot.plant != plant || slot.quantity < quantity)
                {
                    continue;
                }

                slot.quantity -= quantity;
                if (slot.quantity <= 0)
                {
                    ClearSlot(slot);
                }

                OnInventoryChanged?.Invoke();
                return true;
            }

            return false;
        }

        public bool TryAddHarvest(PlantInstance plant)
        {
            if (plant == null || plant.Data == null || plant.State != PlantState.Mature)
            {
                return false;
            }

            EnsureSlots();
            int slotIndex = FindEmptySlot();
            if (slotIndex < 0)
            {
                return false;
            }

            MutationRarity highest = MutationRarity.Common;
            string mutationSummary = "Без мутаций";
            if (plant.Mutations.Count > 0)
            {
                var names = new List<string>(plant.Mutations.Count);
                for (int i = 0; i < plant.Mutations.Count; i++)
                {
                    MutationData mutation = plant.Mutations[i];
                    if (mutation == null)
                    {
                        continue;
                    }

                    names.Add(mutation.mutationName);
                    if ((int)mutation.rarity > (int)highest)
                    {
                        highest = mutation.rarity;
                    }
                }

                if (names.Count > 0)
                {
                    mutationSummary = string.Join(", ", names);
                }
            }

            InventorySlotData slot = _slots[slotIndex];
            slot.kind = InventoryItemKind.Crop;
            slot.plant = plant.Data;
            slot.quantity = 1;
            slot.saleValue = plant.CalculateValue();
            slot.baseSaleValue = slot.saleValue;
            slot.minimumSaleValue = slot.baseSaleValue * 0.1f;
            slot.valueMultiplier = plant.CalculateMutationValueMultiplier();
            slot.mutationCount = plant.Mutations.Count;
            slot.highestRarity = highest;
            slot.mutationSummary = mutationSummary;
            slot.valueBreakdown = plant.BuildValueBreakdown();
            slot.isBurning = plant.HasBurningMutation;

            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool TryRemoveCrop(PlantData plant, out InventorySlotData removedSlot)
        {
            EnsureSlots();
            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlotData slot = _slots[i];
                if (slot.IsEmpty || slot.kind != InventoryItemKind.Crop || slot.plant != plant)
                {
                    continue;
                }

                removedSlot = CloneSlot(slot);
                ClearSlot(slot);
                OnInventoryChanged?.Invoke();
                return true;
            }

            removedSlot = null;
            return false;
        }

        public void RestoreSlots(IEnumerable<InventorySlotData> slots)
        {
            EnsureSlots();
            for (int i = 0; i < _slots.Count; i++)
            {
                ClearSlot(_slots[i]);
            }

            if (slots != null)
            {
                int index = 0;
                foreach (InventorySlotData source in slots)
                {
                    if (source == null || index >= _slots.Count)
                    {
                        index++;
                        continue;
                    }

                    InventorySlotData target = _slots[index];
                    target.kind = source.kind;
                    target.plant = source.plant;
                    target.quantity = source.quantity;
                    target.saleValue = source.saleValue;
                    target.baseSaleValue = source.baseSaleValue;
                    target.minimumSaleValue = source.minimumSaleValue;
                    target.valueMultiplier = source.valueMultiplier;
                    target.mutationCount = source.mutationCount;
                    target.highestRarity = source.highestRarity;
                    target.mutationSummary = source.mutationSummary;
                    target.valueBreakdown = source.valueBreakdown;
                    target.isBurning = source.isBurning;
                    index++;
                }
            }

            OnInventoryChanged?.Invoke();
        }

        public bool TryUnlockWateringCan()
        {
            if (_hasWateringCan)
            {
                return false;
            }

            _hasWateringCan = true;
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void RestoreUtilityState(bool hasWateringCan)
        {
            _hasWateringCan = hasWateringCan;
            OnInventoryChanged?.Invoke();
        }

        public event Action OnInventoryChanged;

        private int CountItems(InventoryItemKind kind, PlantData plant)
        {
            EnsureSlots();
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlotData slot = _slots[i];
                if (!slot.IsEmpty && slot.kind == kind && slot.plant == plant)
                {
                    count += slot.quantity;
                }
            }

            return count;
        }

        private int FindCompatibleSeedSlot(PlantData plant)
        {
            if (plant == null)
            {
                return -1;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlotData slot = _slots[i];
                if (!slot.IsEmpty && slot.kind == InventoryItemKind.Seed && slot.plant == plant)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureSlots()
        {
            while (_slots.Count < _slotCount)
            {
                _slots.Add(new InventorySlotData());
            }

            while (_slots.Count > _slotCount)
            {
                _slots.RemoveAt(_slots.Count - 1);
            }
        }

        private static void ClearSlot(InventorySlotData slot)
        {
            if (slot == null)
            {
                return;
            }

            slot.kind = InventoryItemKind.Seed;
            slot.plant = null;
            slot.quantity = 0;
            slot.saleValue = 0f;
            slot.baseSaleValue = 0f;
            slot.minimumSaleValue = 0f;
            slot.valueMultiplier = 1f;
            slot.mutationCount = 0;
            slot.highestRarity = MutationRarity.Common;
            slot.mutationSummary = string.Empty;
            slot.valueBreakdown = string.Empty;
            slot.isBurning = false;
        }

        private static InventorySlotData CloneSlot(InventorySlotData slot)
        {
            return new InventorySlotData
            {
                kind = slot.kind,
                plant = slot.plant,
                quantity = slot.quantity,
                saleValue = slot.saleValue,
                baseSaleValue = slot.baseSaleValue,
                minimumSaleValue = slot.minimumSaleValue,
                valueMultiplier = slot.valueMultiplier,
                mutationCount = slot.mutationCount,
                highestRarity = slot.highestRarity,
                mutationSummary = slot.mutationSummary,
                valueBreakdown = slot.valueBreakdown,
                isBurning = slot.isBurning
            };
        }

        private void TickBurningSlots(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _burningTickTimer += deltaTime;
            bool changed = false;
            float tickDelta = _burningTickTimer;
            _burningTickTimer = 0f;

            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlotData slot = _slots[i];
                if (slot == null || slot.IsEmpty || !slot.isBurning)
                {
                    continue;
                }

                float minValue = slot.minimumSaleValue > 0f ? slot.minimumSaleValue : Mathf.Max(0.1f, slot.baseSaleValue * 0.1f);
                float nextValue = Mathf.Max(minValue, slot.saleValue * Mathf.Pow(0.99f, tickDelta));
                if (!Mathf.Approximately(nextValue, slot.saleValue))
                {
                    slot.saleValue = nextValue;
                    changed = true;
                }
            }

            if (changed)
            {
                OnInventoryChanged?.Invoke();
            }
        }
    }
}
