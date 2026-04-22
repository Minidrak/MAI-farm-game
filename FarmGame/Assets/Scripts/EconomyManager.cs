using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;

namespace FarmSimulator.Core
{
    public class SaleRecord
    {
        public string PlantName;
        public float  Value;
        public int    MutationCount;
        public MutationRarity HighestRarity;
        public System.DateTime Time;
    }

    public class EconomyManager : MonoBehaviour
    {
        [Header("Starting gold")]
        [SerializeField] private float _startingGold = 200f;

        [Header("Config")]
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private float _wateringCanCost = 60f;

        // ── State ─────────────────────────────────────────────────────────────
        public float Gold { get; private set; }
        public float TotalEarned { get; private set; }

        private readonly List<SaleRecord> _history = new();
        public IReadOnlyList<SaleRecord> History => _history;

        private void Awake() => Gold = _startingGold;

        // ── Sell ──────────────────────────────────────────────────────────────
        public bool Sell(GridCell cell)
        {
            if (cell == null || cell.IsEmpty) return false;
            if (cell.Plant.State != PlantState.Mature) return false;

            float value = cell.Plant.CalculateValue();
            var record  = CreateRecord(cell.Plant, value);

            Gold        += value;
            TotalEarned += value;
            _history.Add(record);

            cell.Plant.Harvest();
            cell.RemovePlant();

            OnSale?.Invoke(record);
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        public bool SellAll(GridManager grid)
        {
            bool anySold = false;
            foreach (var cell in grid.GetAllCells())
                if (Sell(cell)) anySold = true;
            return anySold;
        }

        // ── Spend ─────────────────────────────────────────────────────────────
        public bool TrySpend(float amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        public bool TryBuySeed(PlantData plant)
        {
            if (plant == null) return false;
            return TrySpend(plant.seedCost);
        }

        public bool TryBuySeed(PlantData plant, InventoryManager inventory)
        {
            if (plant == null || inventory == null || !inventory.CanAddSeed(plant))
            {
                return false;
            }

            if (!TryBuySeed(plant))
            {
                return false;
            }

            if (inventory.TryAddSeed(plant))
            {
                return true;
            }

            Gold += plant.seedCost;
            OnGoldChanged?.Invoke(Gold);
            return false;
        }

        public bool TryHarvestToInventory(GridCell cell, InventoryManager inventory)
        {
            if (cell == null || inventory == null || cell.IsEmpty || !cell.IsMature)
            {
                return false;
            }

            if (!inventory.TryAddHarvest(cell.Plant))
            {
                return false;
            }

            cell.Plant.Harvest();
            cell.RemovePlant();
            return true;
        }

        public bool TrySellCropFromInventory(InventoryManager inventory, PlantData plant)
        {
            if (inventory == null || plant == null)
            {
                return false;
            }

            if (!inventory.TryRemoveCrop(plant, out InventorySlotData cropSlot) || cropSlot == null)
            {
                return false;
            }

            float value = Mathf.Max(0f, cropSlot.saleValue);
            Gold += value;
            TotalEarned += value;

            SaleRecord record = new()
            {
                PlantName = cropSlot.plant != null ? cropSlot.plant.plantName : "Урожай",
                Value = value,
                MutationCount = cropSlot.mutationCount,
                HighestRarity = cropSlot.highestRarity,
                Time = System.DateTime.Now
            };

            _history.Add(record);
            OnSale?.Invoke(record);
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        public bool TryBuyWateringCan(InventoryManager inventory)
        {
            if (inventory == null || inventory.HasWateringCan)
            {
                return false;
            }

            if (!TrySpend(_wateringCanCost))
            {
                return false;
            }

            if (inventory.TryUnlockWateringCan())
            {
                return true;
            }

            Gold += _wateringCanCost;
            OnGoldChanged?.Invoke(Gold);
            return false;
        }

        public void RestoreState(float gold, float totalEarned)
        {
            Gold = Mathf.Max(0f, gold);
            TotalEarned = Mathf.Max(0f, totalEarned);
            OnGoldChanged?.Invoke(Gold);
        }

        public void ClearHistory()
        {
            _history.Clear();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private SaleRecord CreateRecord(PlantInstance plant, float value)
        {
            MutationRarity highest = MutationRarity.Common;
            foreach (var m in plant.Mutations)
                if ((int)m.rarity > (int)highest) highest = m.rarity;

            return new SaleRecord
            {
                PlantName      = plant.Data.plantName,
                Value          = value,
                MutationCount  = plant.Mutations.Count,
                HighestRarity  = highest,
                Time           = System.DateTime.Now
            };
        }

        // ── Events ────────────────────────────────────────────────────────────
        public event System.Action<SaleRecord> OnSale;
        public event System.Action<float>      OnGoldChanged;
    }
}
