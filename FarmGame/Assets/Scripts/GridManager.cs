using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;

namespace FarmSimulator.Core
{
    public class GridManager : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private BalanceConfig  _config;
        [SerializeField] private MutationSystem _mutationSystem;

        [Header("Generation")]
        [SerializeField] private bool       _generateAtRuntime;
        [SerializeField] private GameObject _cellPrefab;
        [SerializeField] private float      _cellSpacing = 1.2f;
        [SerializeField] private float      _plotSpacing = 7f;

        // ── Grid ──────────────────────────────────────────────────────────────
        private readonly Dictionary<Vector3Int, GridCell> _cellLookup = new();
        private readonly List<GridCell> _cells = new();

        public int Width  => _config != null ? _config.gridWidth : 0;
        public int Height => _config != null ? _config.gridHeight : 0;
        public int PlotCount => _config != null ? Mathf.Max(1, _config.plotCount) : 0;

        // ── Cardinal offsets ──────────────────────────────────────────────────
        private static readonly (int dx, int dy)[] Cardinals =
            { (0, 1), (0, -1), (-1, 0), (1, 0) };

        // ── Init ──────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_generateAtRuntime)
            {
                BuildGrid();
            }

            RegisterSceneCells();
            RecalcAllNeighborBonuses();
        }

        public void RegisterSceneCells()
        {
            _cellLookup.Clear();
            _cells.Clear();

            GridCell[] sceneCells = GetComponentsInChildren<GridCell>(true);
            foreach (GridCell cell in sceneCells)
            {
                cell.BindRuntime(_config, _mutationSystem);
                SubscribeCell(cell);
                _cellLookup[GetKey(cell.PlotId, cell.X, cell.Y)] = cell;
                _cells.Add(cell);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────
        public GridCell RegisterCell(GridCell cell, int plotId, int x, int y, bool resetSoil = false)
        {
            if (cell == null)
            {
                return null;
            }

            cell.Initialize(plotId, x, y, _config, _mutationSystem, resetSoil);
            SubscribeCell(cell);
            _cellLookup[GetKey(plotId, x, y)] = cell;

            if (!_cells.Contains(cell))
            {
                _cells.Add(cell);
            }

            return cell;
        }

        public GridCell GetCell(int plotId, int x, int y)
        {
            _cellLookup.TryGetValue(GetKey(plotId, x, y), out GridCell cell);
            return cell;
        }

        public GridCell GetCell(int x, int y)
        {
            return GetCell(0, x, y);
        }

        public GridCell GetCellByWorldReference(GridCell reference)
        {
            return reference == null ? null : GetCell(reference.PlotId, reference.X, reference.Y);
        }

        public bool TryPlant(int plotId, int x, int y, PlantData data)
        {
            GridCell cell = GetCell(plotId, x, y);
            if (cell == null) return false;

            bool planted = cell.TryPlant(data);
            if (planted) RecalcNeighborsOf(cell);
            return planted;
        }

        public bool TryPlant(int x, int y, PlantData data)
        {
            return TryPlant(0, x, y, data);
        }

        public PlantInstance RemovePlant(int plotId, int x, int y)
        {
            GridCell cell = GetCell(plotId, x, y);
            PlantInstance plant = cell?.RemovePlant();
            if (plant != null) RecalcNeighborsOf(cell);
            return plant;
        }

        public PlantInstance RemovePlant(int x, int y)
        {
            return RemovePlant(0, x, y);
        }

        public IEnumerable<GridCell> GetAllCells()
        {
            return _cells;
        }

        // ── Neighbors ─────────────────────────────────────────────────────────
        public List<GridCell> GetNeighbors(GridCell cell)
        {
            var result = new List<GridCell>(4);
            if (cell == null) return result;

            foreach (var (dx, dy) in Cardinals)
            {
                var neighbor = GetCell(cell.PlotId, cell.X + dx, cell.Y + dy);
                if (neighbor != null && !neighbor.IsEmpty)
                    result.Add(neighbor);
            }
            return result;
        }

        public (float growthBonus, float yieldBonus, float valueBonus) CalculatePredictedNeighborBonus(GridCell cell, PlantData plantData)
        {
            if (cell == null || plantData == null)
            {
                return (0f, 0f, 0f);
            }

            var neighbors = GetNeighbors(cell);
            if (neighbors.Count == 0)
            {
                return (0f, 0f, 0f);
            }

            float totalGrowth = 0f;
            float totalYield = 0f;
            float totalValue = 0f;
            float decayFactor = 1f;
            int conflictCount = 0;

            foreach (var neighbor in neighbors)
            {
                if (neighbor == null || neighbor.IsEmpty || neighbor.Plant == null || neighbor.Plant.Data == null)
                {
                    continue;
                }

                PlantGroup theirGroup = neighbor.Plant.Data.group;
                float interaction = GetInteractionScore(plantData, theirGroup);

                if (interaction >= 0f)
                {
                    totalGrowth += interaction * decayFactor;
                    totalYield += interaction * decayFactor;
                    totalValue += interaction * 0.75f * decayFactor;
                }
                else
                {
                    float severity = Mathf.Abs(interaction) * decayFactor;
                    totalGrowth -= severity * 1.8f;
                    totalYield -= severity * 2.25f;
                    totalValue -= severity * 2.75f;
                    conflictCount++;

                    if (plantData.group == PlantGroup.Exotic && theirGroup == PlantGroup.Vegetables)
                    {
                        totalYield -= severity * 0.9f;
                        totalValue -= severity * 1.2f;
                    }
                }

                decayFactor *= _config.bonusStackDecay;
            }

            if (conflictCount >= 2)
            {
                totalGrowth -= 0.15f * conflictCount;
                totalYield -= 0.25f * conflictCount;
                totalValue -= 0.35f * conflictCount;
            }

            return (totalGrowth, totalYield, totalValue);
        }

        // ── Bonus Calculation ─────────────────────────────────────────────────
        /// <summary>
        /// Calculates non-linear neighbor bonus for a cell.
        /// Applies synergy/conflict based on plant groups.
        /// Returns (growthBonus, yieldBonus, valueBonus).
        /// </summary>
        public (float growthBonus, float yieldBonus, float valueBonus) CalculateNeighborBonus(GridCell cell)
        {
            if (cell == null || cell.IsEmpty) return (0f, 0f, 0f);

            var neighbors = GetNeighbors(cell);
            if (neighbors.Count == 0) return (0f, 0f, 0f);
            float totalGrowth  = 0f;
            float totalYield   = 0f;
            float totalValue   = 0f;
            float decayFactor  = 1f;
            int conflictCount = 0;

            foreach (var neighbor in neighbors)
            {
                PlantGroup theirGroup = neighbor.Plant.Data.group;
                float interaction     = GetInteractionScore(cell.Plant.Data, theirGroup);

                if (interaction >= 0f)
                {
                    totalGrowth += interaction * decayFactor;
                    totalYield  += interaction * decayFactor;
                    totalValue  += interaction * 0.75f * decayFactor;
                }
                else
                {
                    float severity = Mathf.Abs(interaction) * decayFactor;
                    totalGrowth -= severity * 1.8f;
                    totalYield  -= severity * 2.25f;
                    totalValue  -= severity * 2.75f;
                    conflictCount++;

                    if (cell.Plant.Data.group == PlantGroup.Exotic && neighbor.Plant.Data.group == PlantGroup.Vegetables)
                    {
                        totalYield -= severity * 0.9f;
                        totalValue -= severity * 1.2f;
                    }
                }

                decayFactor *= _config.bonusStackDecay;
            }

            if (conflictCount >= 2)
            {
                totalGrowth -= 0.15f * conflictCount;
                totalYield  -= 0.25f * conflictCount;
                totalValue  -= 0.35f * conflictCount;
            }

            return (totalGrowth, totalYield, totalValue);
        }

        private float GetInteractionScore(PlantData source, PlantGroup targetGroup)
        {
            foreach (var syn in source.synergisticGroups)
                if (syn == targetGroup)
                    return _config.synergyBonus;

            foreach (var con in source.conflictingGroups)
                if (con == targetGroup)
                    return -_config.conflictPenalty;

            return 0f;
        }

        // ── Apply Neighbor Bonuses to Plant ───────────────────────────────────
        public void ApplyNeighborBonusToCell(GridCell cell)
        {
            if (cell == null || cell.IsEmpty) return;

            var (growthBonus, yieldBonus, valueBonus) = CalculateNeighborBonus(cell);
            cell.Plant.GrowthRateModifier = growthBonus;
            cell.Plant.YieldModifier      = yieldBonus;
            cell.Plant.ValueModifier      = valueBonus;
        }

        // ── Plant Density ─────────────────────────────────────────────────────
        public float GetPlantDensity()
        {
            if (_cells.Count == 0) return 0f;

            int occupied = 0;
            foreach (var cell in GetAllCells())
                if (!cell.IsEmpty) occupied++;
            return (float)occupied / _cells.Count;
        }

        // ── Recalc ────────────────────────────────────────────────────────────
        private void RecalcNeighborsOf(GridCell cell)
        {
            if (cell == null) return;

            if (!cell.IsEmpty)
            {
                cell.Plant.GrowthRateModifier = 0f;
                cell.Plant.YieldModifier      = 0f;
                cell.Plant.ValueModifier      = 0f;
            }

            // Reset all modifier contributions from neighbors
            foreach (var neighbor in GetNeighbors(cell))
            {
                if (neighbor.IsEmpty) continue;
                neighbor.Plant.GrowthRateModifier = 0f;
                neighbor.Plant.YieldModifier      = 0f;
                neighbor.Plant.ValueModifier      = 0f;
            }

            // Recalculate for cell and its neighbors
            ApplyNeighborBonusToCell(cell);
            foreach (var neighbor in GetNeighbors(cell))
                ApplyNeighborBonusToCell(neighbor);
        }

        public void RecalcAllNeighborBonuses()
        {
            // Clear modifiers
            foreach (var cell in GetAllCells())
            {
                if (cell.IsEmpty) continue;
                cell.Plant.GrowthRateModifier = 0f;
                cell.Plant.YieldModifier      = 0f;
                cell.Plant.ValueModifier      = 0f;
            }

            // Apply bonuses
            foreach (var cell in GetAllCells())
                ApplyNeighborBonusToCell(cell);
        }

        private void BuildGrid()
        {
            if (_config == null) return;

            for (int plotId = 0; plotId < PlotCount; plotId++)
            {
                Vector3 plotOffset = new(plotId * _plotSpacing, 0f, 0f);

                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Vector3 pos = transform.position + plotOffset + new Vector3(x * _cellSpacing, 0f, y * _cellSpacing);

                        GameObject go = _cellPrefab != null
                            ? Instantiate(_cellPrefab, pos, Quaternion.identity, transform)
                            : new GameObject($"Cell_{plotId}_{x}_{y}");

                        go.transform.position = pos;

                        GridCell cell = go.GetComponent<GridCell>() ?? go.AddComponent<GridCell>();
                        RegisterCell(cell, plotId, x, y, resetSoil: true);
                    }
                }
            }
        }

        private void SubscribeCell(GridCell cell)
        {
            cell.OnPlanted -= HandleCellPlanted;
            cell.OnPlanted += HandleCellPlanted;
            cell.OnPlantRemoved -= HandleCellRemoved;
            cell.OnPlantRemoved += HandleCellRemoved;
        }

        private void HandleCellPlanted(GridCell cell, PlantInstance _)
        {
            RecalcNeighborsOf(cell);
        }

        private void HandleCellRemoved(GridCell cell)
        {
            RecalcNeighborsOf(cell);
        }

        private static Vector3Int GetKey(int plotId, int x, int y)
        {
            return new Vector3Int(plotId, x, y);
        }
    }
}
