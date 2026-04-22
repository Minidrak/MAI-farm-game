using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using FarmSimulator.Core;
using FarmSimulator.Data;

namespace FarmSimulator.Player
{
    public class InteractionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private EconomyManager _economy;
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private Renderer _highlightRenderer;
        [SerializeField] private WorldInteractionZone[] _interactionZones;

        [Header("Catalog")]
        [SerializeField] private PlantData[] _availablePlants;

        private GridCell _currentCell;
        private GridCell _nearestCell;
        private WorldInteractionZone _currentZone;
        private int _selectedPlantIndex;

        public GridCell CurrentCell => _currentCell;
        public GridCell NearestCell => _nearestCell;
        public WorldInteractionZone CurrentZone => _currentZone;
        public PlantData SelectedPlant => _availablePlants != null && _availablePlants.Length > 0
            ? _availablePlants[Mathf.Clamp(_selectedPlantIndex, 0, _availablePlants.Length - 1)]
            : null;

        public IReadOnlyList<PlantData> AvailablePlants => _availablePlants;

        private void Awake()
        {
            SortAvailablePlants();

            if (_highlightRenderer != null)
            {
                _highlightRenderer.gameObject.SetActive(false);
            }

            if ((_interactionZones == null || _interactionZones.Length == 0) && Application.isPlaying)
            {
                _interactionZones = FindObjectsByType<WorldInteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
        }

        private void Update()
        {
            RefreshCurrentTarget();
            CycleSelectedPlant();

            if (ReadInteractPressed())
            {
                Interact();
            }

            if (ReadUtilityPressed())
            {
                InteractWithUtility();
            }
        }

        public void SetSelectedPlant(int index)
        {
            if (_availablePlants == null || _availablePlants.Length == 0)
            {
                _selectedPlantIndex = 0;
                return;
            }

            _selectedPlantIndex = Mathf.Clamp(index, 0, _availablePlants.Length - 1);
        }

        public string GetInteractionPrompt()
        {
            if (_currentZone != null)
            {
                PlantData selected = SelectedPlant;
                if (selected == null)
                {
                    return "Нет выбранной культуры";
                }

                return _currentZone.ZoneType switch
                {
                    WorldInteractionZoneType.Shop => BuildShopPrompt(selected),
                    WorldInteractionZoneType.SellStall => _inventory != null && _inventory.GetCropCount(selected) > 0
                        ? $"E: продать {selected.plantName}"
                        : $"В инвентаре нет {selected.plantName}",
                    _ => $"E: взаимодействовать с {_currentZone.DisplayName}"
                };
            }

            if (_currentCell == null)
            {
                return "Подойдите к грядке, магазину или лавке";
            }

            if (_currentCell.IsDestroyed)
            {
                return $"E: восстановить грядку ({GridCell.RepairCost:F0}g)";
            }

            if (_currentCell.RequiresWatering)
            {
                return _inventory != null && _inventory.HasWateringCan
                    ? "E: полить грядку"
                    : "Нужна канистра из магазина";
            }

            if (_currentCell.IsEmpty)
            {
                PlantData plant = SelectedPlant;
                if (plant == null)
                {
                    return "Нет выбранного семени";
                }

                int seedCount = _inventory != null ? _inventory.GetSeedCount(plant) : 0;
                return seedCount > 0
                    ? $"E: посадить {plant.plantName} (в рюкзаке: {seedCount})"
                    : $"Сначала купите {plant.plantName} в магазине";
            }

            if (_currentCell.IsMature)
            {
                return _inventory != null && _inventory.OccupiedSlotsCount < _inventory.SlotCount
                    ? $"E: собрать {_currentCell.Plant.Data.plantName}"
                    : "Инвентарь заполнен";
            }

            return $"E: осмотреть {_currentCell.Plant.Data.plantName}";
        }

        public void Interact()
        {
            if (_currentZone != null)
            {
                InteractWithZone();
                return;
            }

            if (_currentCell == null)
            {
                return;
            }

            if (_currentCell.IsDestroyed)
            {
                if (_economy.TrySpend(GridCell.RepairCost))
                {
                    _currentCell.RestoreBed();
                    OnCellInteracted?.Invoke(_currentCell);
                }
                return;
            }

            if (_currentCell.RequiresWatering)
            {
                if (_inventory != null && _inventory.HasWateringCan)
                {
                    _currentCell.Water();
                    OnCellInteracted?.Invoke(_currentCell);
                }
                return;
            }

            if (_currentCell.IsEmpty)
            {
                PlantData selected = SelectedPlant;
                if (selected != null && _inventory != null && _inventory.TryConsumeSeed(selected))
                {
                    _currentCell.TryPlant(selected);
                    _gridManager.RecalcAllNeighborBonuses();
                    OnCellInteracted?.Invoke(_currentCell);
                }

                return;
            }

            if (_currentCell.IsMature)
            {
                if (_economy.TryHarvestToInventory(_currentCell, _inventory))
                {
                    _gridManager.RecalcAllNeighborBonuses();
                    OnCellInteracted?.Invoke(_currentCell);
                }
                return;
            }

            OnCellInteracted?.Invoke(_currentCell);
        }

        private void InteractWithZone()
        {
            PlantData selected = SelectedPlant;
            if (_currentZone == null || selected == null)
            {
                return;
            }

            switch (_currentZone.ZoneType)
            {
                case WorldInteractionZoneType.Shop:
                    if (_economy.TryBuySeed(selected, _inventory))
                    {
                        OnCellInteracted?.Invoke(null);
                    }
                    break;
                case WorldInteractionZoneType.SellStall:
                    if (_economy.TrySellCropFromInventory(_inventory, selected))
                    {
                        OnCellInteracted?.Invoke(null);
                    }
                    break;
            }
        }

        private void InteractWithUtility()
        {
            if (_currentZone == null || _currentZone.ZoneType != WorldInteractionZoneType.Shop || _inventory == null)
            {
                return;
            }

            if (_economy.TryBuyWateringCan(_inventory))
            {
                OnCellInteracted?.Invoke(null);
            }
        }

        private string BuildShopPrompt(PlantData selected)
        {
            string seedPrompt = selected != null
                ? $"E: купить семя {selected.plantName} ({selected.seedCost:F0}g)"
                : "Нет выбранной культуры";

            if (_inventory == null || _inventory.HasWateringCan)
            {
                return seedPrompt;
            }

            return $"{seedPrompt}\nF: купить канистру (60g)";
        }

        private void CycleSelectedPlant()
        {
            if (_availablePlants == null || _availablePlants.Length == 0)
            {
                return;
            }

            if (ReadPreviousSeedPressed())
            {
                _selectedPlantIndex = (_selectedPlantIndex - 1 + _availablePlants.Length) % _availablePlants.Length;
            }
            else if (ReadNextSeedPressed())
            {
                _selectedPlantIndex = (_selectedPlantIndex + 1) % _availablePlants.Length;
            }
        }

        private void RefreshCurrentTarget()
        {
            _currentCell = null;
            _nearestCell = null;
            _currentZone = null;

            float range = _config != null ? _config.interactionRange : 2f;
            float minCellDistance = range;
            float minZoneDistance = range;

            foreach (GridCell cell in _gridManager.GetAllCells())
            {
                float distance = Vector3.Distance(transform.position, cell.transform.position);
                if (distance <= minCellDistance)
                {
                    minCellDistance = distance;
                    _nearestCell = cell;
                    _currentCell = cell;
                }
            }

            if (_interactionZones != null)
            {
                for (int i = 0; i < _interactionZones.Length; i++)
                {
                    WorldInteractionZone zone = _interactionZones[i];
                    if (zone == null || !zone.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(transform.position, zone.transform.position);
                    if (distance <= minZoneDistance)
                    {
                        minZoneDistance = distance;
                        _currentZone = zone;
                    }
                }
            }

            if (_currentZone != null && (_currentCell == null || minZoneDistance <= minCellDistance))
            {
                _currentCell = null;
            }
        }

        private void SortAvailablePlants()
        {
            if (_availablePlants == null || _availablePlants.Length < 2)
            {
                return;
            }

            System.Array.Sort(_availablePlants, (left, right) =>
            {
                if (left == null && right == null) return 0;
                if (left == null) return 1;
                if (right == null) return -1;

                int orderCompare = left.displayOrder.CompareTo(right.displayOrder);
                return orderCompare != 0
                    ? orderCompare
                    : string.Compare(left.plantName, right.plantName, System.StringComparison.Ordinal);
            });
        }

        private bool ReadInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private bool ReadPreviousSeedPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Q);
#endif
        }

        private bool ReadNextSeedPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private bool ReadUtilityPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        public event System.Action<GridCell> OnCellInteracted;
    }
}
