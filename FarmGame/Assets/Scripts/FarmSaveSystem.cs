using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FarmSimulator.Core;
using FarmSimulator.Data;

namespace FarmSimulator
{
    [Serializable]
    public class ActiveEventSaveData
    {
        public string eventName;
        public float timeRemaining;
    }

    [Serializable]
    public class PlantSaveData
    {
        public string plantName;
        public float growth;
        public string state;
        public float burningValueFactor = 1f;
        public List<string> mutations = new();
    }

    [Serializable]
    public class CellSaveData
    {
        public int plotId;
        public int x;
        public int y;
        public float fertility;
        public float moisture;
        public float infection;
        public bool requiresWatering;
        public bool isDestroyed;
        public PlantSaveData plant;
    }

    [Serializable]
    public class InventorySlotSaveData
    {
        public string itemKind;
        public string plantName;
        public int quantity;
        public float saleValue;
        public float baseSaleValue;
        public float minimumSaleValue;
        public float valueMultiplier;
        public int mutationCount;
        public string highestRarity;
        public string mutationSummary;
        public string valueBreakdown;
        public bool isBurning;
    }

    [Serializable]
    public class FarmSaveData
    {
        public float gold;
        public float totalEarned;
        public float nextEventTimer;
        public bool hasWateringCan;
        public List<ActiveEventSaveData> activeEvents = new();
        public List<CellSaveData> cells = new();
        public List<InventorySlotSaveData> inventory = new();
    }

    public class FarmSaveSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private EconomyManager _economy;
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private InventoryManager _inventory;

        [Header("Catalog")]
        [SerializeField] private PlantData[] _plants;
        [SerializeField] private MutationData[] _mutations;

        private string SavePath => Path.Combine(Application.persistentDataPath, _config.saveSlotName);

        public bool HasSave()
        {
            return _config != null && File.Exists(SavePath);
        }

        public void Save()
        {
            if (_config == null || _gridManager == null || _economy == null || _eventSystem == null)
            {
                return;
            }

            FarmSaveData save = new()
            {
                gold = _economy.Gold,
                totalEarned = _economy.TotalEarned,
                nextEventTimer = _eventSystem.NextEventTimer,
                hasWateringCan = _inventory != null && _inventory.HasWateringCan
            };

            foreach (ActiveEvent active in _eventSystem.ActiveEvents)
            {
                if (active?.Data == null) continue;
                save.activeEvents.Add(new ActiveEventSaveData
                {
                    eventName = active.Data.eventName,
                    timeRemaining = active.TimeRemaining
                });
            }

            foreach (GridCell cell in _gridManager.GetAllCells())
            {
                CellSaveData cellSave = new()
                {
                    plotId = cell.PlotId,
                    x = cell.X,
                    y = cell.Y,
                    fertility = cell.Soil.fertility,
                    moisture = cell.Soil.moisture,
                    infection = cell.Soil.infection,
                    requiresWatering = cell.RequiresWatering,
                    isDestroyed = cell.IsDestroyed
                };

                if (!cell.IsEmpty)
                {
                    PlantSaveData plantSave = new()
                    {
                        plantName = cell.Plant.Data.plantName,
                        growth = cell.Plant.Growth,
                        state = cell.Plant.State.ToString(),
                        burningValueFactor = cell.Plant.BurningValueFactor
                    };

                    foreach (MutationData mutation in cell.Plant.Mutations)
                    {
                        if (mutation != null)
                        {
                            plantSave.mutations.Add(mutation.mutationName);
                        }
                    }

                    cellSave.plant = plantSave;
                }

                save.cells.Add(cellSave);
            }

            if (_inventory != null)
            {
                foreach (InventorySlotData slot in _inventory.Slots)
                {
                    if (slot == null || slot.IsEmpty)
                    {
                        save.inventory.Add(new InventorySlotSaveData());
                        continue;
                    }

                    save.inventory.Add(new InventorySlotSaveData
                    {
                        itemKind = slot.kind.ToString(),
                        plantName = slot.plant != null ? slot.plant.plantName : string.Empty,
                        quantity = slot.quantity,
                        saleValue = slot.saleValue,
                        baseSaleValue = slot.baseSaleValue,
                        minimumSaleValue = slot.minimumSaleValue,
                        valueMultiplier = slot.valueMultiplier,
                        mutationCount = slot.mutationCount,
                        highestRarity = slot.highestRarity.ToString(),
                        mutationSummary = slot.mutationSummary,
                        valueBreakdown = slot.valueBreakdown,
                        isBurning = slot.isBurning
                    });
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? Application.persistentDataPath);
            File.WriteAllText(SavePath, JsonUtility.ToJson(save, true));
        }

        public bool Load()
        {
            if (!HasSave())
            {
                return false;
            }

            FarmSaveData save = JsonUtility.FromJson<FarmSaveData>(File.ReadAllText(SavePath));
            if (save == null)
            {
                return false;
            }

            _economy.RestoreState(save.gold, save.totalEarned);

            List<ActiveEvent> restoredEvents = new();
            foreach (ActiveEventSaveData activeEventSave in save.activeEvents)
            {
                EventData eventData = _eventSystem.FindEventByName(activeEventSave.eventName);
                if (eventData == null) continue;

                restoredEvents.Add(new ActiveEvent
                {
                    Data = eventData,
                    TimeRemaining = activeEventSave.timeRemaining
                });
            }

            _eventSystem.RestoreState(restoredEvents, save.nextEventTimer);

            foreach (CellSaveData cellSave in save.cells)
            {
                GridCell cell = _gridManager.GetCell(cellSave.plotId, cellSave.x, cellSave.y);
                if (cell == null) continue;

                cell.SetSoil(cellSave.fertility, cellSave.moisture, cellSave.infection);
                if (cellSave.isDestroyed)
                {
                    cell.DestroyBed();
                }
                else
                {
                    cell.RestoreBed();
                }
                cell.SetRequiresWatering(cellSave.requiresWatering && !cellSave.isDestroyed);

                if (cellSave.plant == null)
                {
                    if (!cell.IsEmpty)
                    {
                        cell.RemovePlant();
                    }

                    continue;
                }

                PlantData plantData = FindPlant(cellSave.plant.plantName);
                if (plantData == null) continue;

                List<MutationData> mutations = new();
                foreach (string mutationName in cellSave.plant.mutations)
                {
                    MutationData mutation = FindMutation(mutationName);
                    if (mutation != null)
                    {
                        mutations.Add(mutation);
                    }
                }

                PlantState state = Enum.TryParse(cellSave.plant.state, out PlantState parsedState)
                    ? parsedState
                    : PlantState.Growing;

                cell.RestorePlant(plantData, mutations, cellSave.plant.growth, state, cellSave.plant.burningValueFactor);
            }

            if (_inventory != null)
            {
                List<InventorySlotData> restoredSlots = new();
                foreach (InventorySlotSaveData slotSave in save.inventory)
                {
                    InventorySlotData slot = new();
                    if (!string.IsNullOrWhiteSpace(slotSave.plantName))
                    {
                        slot.plant = FindPlant(slotSave.plantName);
                        slot.quantity = slotSave.quantity;
                        slot.saleValue = slotSave.saleValue;
                        slot.baseSaleValue = slotSave.baseSaleValue;
                        slot.minimumSaleValue = slotSave.minimumSaleValue;
                        slot.valueMultiplier = slotSave.valueMultiplier <= 0f ? 1f : slotSave.valueMultiplier;
                        slot.mutationCount = slotSave.mutationCount;
                        slot.mutationSummary = slotSave.mutationSummary;
                        slot.valueBreakdown = slotSave.valueBreakdown;
                        slot.isBurning = slotSave.isBurning;
                        if (Enum.TryParse(slotSave.itemKind, out InventoryItemKind itemKind))
                        {
                            slot.kind = itemKind;
                        }
                        if (Enum.TryParse(slotSave.highestRarity, out MutationRarity rarity))
                        {
                            slot.highestRarity = rarity;
                        }
                    }
                    restoredSlots.Add(slot);
                }
                _inventory.RestoreSlots(restoredSlots);
                _inventory.RestoreUtilityState(save.hasWateringCan);
            }

            _gridManager.RecalcAllNeighborBonuses();
            return true;
        }

        public void DeleteSave()
        {
            if (HasSave())
            {
                File.Delete(SavePath);
            }
        }

        private PlantData FindPlant(string plantName)
        {
            foreach (PlantData plant in _plants)
            {
                if (plant != null && plant.plantName == plantName)
                {
                    return plant;
                }
            }

            return null;
        }

        private MutationData FindMutation(string mutationName)
        {
            foreach (MutationData mutation in _mutations)
            {
                if (mutation != null && mutation.mutationName == mutationName)
                {
                    return mutation;
                }
            }

            return null;
        }
    }
}
