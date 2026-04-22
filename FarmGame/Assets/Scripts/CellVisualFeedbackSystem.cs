using System.Collections.Generic;
using FarmSimulator.Core;
using FarmSimulator.Player;
using UnityEngine;

namespace FarmSimulator.Visual
{
    public class CellVisualFeedbackSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private InteractionController _interactionController;
        [SerializeField] private Renderer _selectionHighlight;

        [Header("Selection Highlight")]
        [SerializeField] private Color _emptyCellColor = new(0.22f, 0.91f, 0.42f, 0.72f);
        [SerializeField] private Color _growingCellColor = new(0.22f, 0.68f, 1f, 0.72f);
        [SerializeField] private Color _matureCellColor = new(1f, 0.76f, 0.18f, 0.82f);
        [SerializeField] private Color _wateringCellColor = new(0.26f, 0.86f, 1f, 0.86f);
        [SerializeField] private Color _destroyedCellColor = new(1f, 0.34f, 0.3f, 0.9f);
        [SerializeField] private float _highlightHeight = 0.06f;
        [SerializeField] private float _highlightPulseAmplitude = 0.08f;
        [SerializeField] private float _highlightPulseSpeed = 4.5f;

        [Header("Mature Indicators")]
        [SerializeField] private Color _indicatorColor = new(1f, 0.79f, 0.2f, 0.95f);
        [SerializeField] private Vector3 _indicatorWorldOffset = new(0f, 0.6f, 0f);
        [SerializeField] private Vector3 _indicatorScale = new(0.32f, 0.32f, 0.32f);
        [SerializeField] private float _indicatorBobAmplitude = 0.12f;
        [SerializeField] private float _indicatorBobSpeed = 2.4f;

        [Header("State Indicators")]
        [SerializeField] private Vector3 _wateringIndicatorScale = new(0.7f, 0.04f, 0.7f);
        [SerializeField] private Vector3 _destroyedIndicatorScale = new(0.82f, 0.06f, 0.12f);
        [SerializeField] private float _stateIndicatorHeight = 0.08f;
        [SerializeField] private float _stateIndicatorBobSpeed = 3.2f;
        [SerializeField] private float _stateIndicatorBobAmplitude = 0.04f;

        [Header("Neighbor Indicators")]
        [SerializeField] private Color _synergyIndicatorColor = new(0.3f, 1f, 0.42f, 0.95f);
        [SerializeField] private Color _conflictIndicatorColor = new(1f, 0.28f, 0.22f, 0.95f);
        [SerializeField] private Vector3 _neighborIndicatorScale = new(0.24f, 0.24f, 0.24f);
        [SerializeField] private float _neighborIndicatorHeight = 0.18f;
        [SerializeField] private float _neighborIndicatorBobSpeed = 2.8f;
        [SerializeField] private float _neighborIndicatorBobAmplitude = 0.05f;

        private readonly Dictionary<GridCell, Transform> _matureIndicators = new();
        private readonly Dictionary<GridCell, Transform> _wateringIndicators = new();
        private readonly Dictionary<GridCell, Transform> _destroyedIndicators = new();
        private readonly Dictionary<GridCell, Transform> _neighborIndicators = new();
        private Material _highlightMaterialInstance;
        private Material _indicatorMaterialTemplate;
        private Material _wateringIndicatorMaterial;
        private Material _destroyedIndicatorMaterial;
        private Material _synergyIndicatorMaterial;
        private Material _conflictIndicatorMaterial;
        private Vector3 _highlightBaseScale = Vector3.one;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (_selectionHighlight != null)
            {
                _highlightBaseScale = _selectionHighlight.transform.localScale;
                _highlightMaterialInstance = new Material(_selectionHighlight.sharedMaterial);
                _selectionHighlight.sharedMaterial = _highlightMaterialInstance;
                _selectionHighlight.gameObject.SetActive(false);
            }

            _indicatorMaterialTemplate = CreateRuntimeMaterial("CellMatureIndicatorRuntime", _indicatorColor);
            _wateringIndicatorMaterial = CreateRuntimeMaterial("CellWateringIndicatorRuntime", _wateringCellColor);
            _destroyedIndicatorMaterial = CreateRuntimeMaterial("CellDestroyedIndicatorRuntime", _destroyedCellColor);
            _synergyIndicatorMaterial = CreateRuntimeMaterial("CellSynergyIndicatorRuntime", _synergyIndicatorColor);
            _conflictIndicatorMaterial = CreateRuntimeMaterial("CellConflictIndicatorRuntime", _conflictIndicatorColor);
        }

        private void LateUpdate()
        {
            RefreshSelectionHighlight();
            RefreshMatureIndicators();
            RefreshStateIndicators();
            RefreshNeighborIndicators();
        }

        private void OnDisable()
        {
            if (_selectionHighlight != null)
            {
                _selectionHighlight.gameObject.SetActive(false);
            }
        }

        private void RefreshSelectionHighlight()
        {
            if (_selectionHighlight == null || _interactionController == null)
            {
                return;
            }

            GridCell currentCell = _interactionController.CurrentCell;
            if (currentCell == null)
            {
                _selectionHighlight.gameObject.SetActive(false);
                return;
            }

            _selectionHighlight.gameObject.SetActive(true);
            _selectionHighlight.transform.position = currentCell.transform.position + Vector3.up * _highlightHeight;

            float pulse = 1f + Mathf.Sin(Time.time * _highlightPulseSpeed) * _highlightPulseAmplitude;
            _selectionHighlight.transform.localScale = _highlightBaseScale * pulse;

            Color color;
            if (currentCell.IsDestroyed)
            {
                color = _destroyedCellColor;
            }
            else if (currentCell.RequiresWatering)
            {
                color = _wateringCellColor;
            }
            else
            {
                color = currentCell.IsEmpty
                    ? _emptyCellColor
                    : currentCell.IsMature
                        ? _matureCellColor
                        : _growingCellColor;
            }

            ApplyMaterialColor(_highlightMaterialInstance, color, color * 1.2f);
        }

        private void RefreshMatureIndicators()
        {
            if (_gridManager == null)
            {
                return;
            }

            HashSet<GridCell> activeMatureCells = new();
            foreach (GridCell cell in _gridManager.GetAllCells())
            {
                if (cell == null || !cell.IsMature)
                {
                    continue;
                }

                activeMatureCells.Add(cell);

                if (!_matureIndicators.TryGetValue(cell, out Transform indicator) || indicator == null)
                {
                    indicator = CreateIndicator(cell);
                    _matureIndicators[cell] = indicator;
                }

                UpdateIndicator(cell, indicator);
            }

            if (_matureIndicators.Count == 0)
            {
                return;
            }

            List<GridCell> cellsToRemove = null;
            foreach ((GridCell cell, Transform indicator) in _matureIndicators)
            {
                if (activeMatureCells.Contains(cell) && indicator != null)
                {
                    continue;
                }

                cellsToRemove ??= new List<GridCell>();
                cellsToRemove.Add(cell);

                if (indicator != null)
                {
                    Destroy(indicator.gameObject);
                }
            }

            if (cellsToRemove == null)
            {
                return;
            }

            foreach (GridCell cell in cellsToRemove)
            {
                _matureIndicators.Remove(cell);
            }
        }

        private void RefreshStateIndicators()
        {
            if (_gridManager == null)
            {
                return;
            }

            HashSet<GridCell> wateringCells = new();
            HashSet<GridCell> destroyedCells = new();

            foreach (GridCell cell in _gridManager.GetAllCells())
            {
                if (cell == null)
                {
                    continue;
                }

                if (cell.RequiresWatering && !cell.IsDestroyed)
                {
                    wateringCells.Add(cell);
                    if (!_wateringIndicators.TryGetValue(cell, out Transform wateringIndicator) || wateringIndicator == null)
                    {
                        wateringIndicator = CreateWateringIndicator(cell);
                        _wateringIndicators[cell] = wateringIndicator;
                    }

                    UpdateWateringIndicator(cell, wateringIndicator);
                }

                if (cell.IsDestroyed)
                {
                    destroyedCells.Add(cell);
                    if (!_destroyedIndicators.TryGetValue(cell, out Transform destroyedIndicator) || destroyedIndicator == null)
                    {
                        destroyedIndicator = CreateDestroyedIndicator(cell);
                        _destroyedIndicators[cell] = destroyedIndicator;
                    }

                    UpdateDestroyedIndicator(cell, destroyedIndicator);
                }
            }

            CleanupIndicators(_wateringIndicators, wateringCells);
            CleanupIndicators(_destroyedIndicators, destroyedCells);
        }

        private void RefreshNeighborIndicators()
        {
            if (_gridManager == null)
            {
                return;
            }

            HashSet<GridCell> activeCells = new();
            foreach (GridCell cell in _gridManager.GetAllCells())
            {
                if (cell == null || cell.IsEmpty || cell.IsDestroyed)
                {
                    continue;
                }

                float relationScore = cell.Plant.GrowthRateModifier + cell.Plant.YieldModifier + cell.Plant.ValueModifier;
                if (Mathf.Abs(relationScore) < 0.05f)
                {
                    continue;
                }

                activeCells.Add(cell);
                if (!_neighborIndicators.TryGetValue(cell, out Transform indicator) || indicator == null)
                {
                    indicator = CreateNeighborIndicator(cell, relationScore > 0f);
                    _neighborIndicators[cell] = indicator;
                }

                UpdateNeighborIndicator(cell, indicator, relationScore);
            }

            CleanupIndicators(_neighborIndicators, activeCells);
        }

        private Transform CreateIndicator(GridCell cell)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "MatureIndicator";
            indicator.transform.SetParent(cell.transform, false);
            indicator.transform.localScale = _indicatorScale;
            indicator.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null && _indicatorMaterialTemplate != null)
            {
                renderer.sharedMaterial = new Material(_indicatorMaterialTemplate);
            }

            return indicator.transform;
        }

        private Transform CreateWateringIndicator(GridCell cell)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "WateringIndicator";
            indicator.transform.SetParent(cell.transform, false);
            indicator.transform.localScale = _wateringIndicatorScale;

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null && _wateringIndicatorMaterial != null)
            {
                renderer.sharedMaterial = new Material(_wateringIndicatorMaterial);
            }

            return indicator.transform;
        }

        private Transform CreateDestroyedIndicator(GridCell cell)
        {
            GameObject root = new("DestroyedIndicator");
            root.transform.SetParent(cell.transform, false);

            Transform slashA = CreateDestroyedSlash(root.transform, "SlashA");
            Transform slashB = CreateDestroyedSlash(root.transform, "SlashB");
            slashA.localRotation = Quaternion.Euler(0f, 45f, 0f);
            slashB.localRotation = Quaternion.Euler(0f, -45f, 0f);
            return root.transform;
        }

        private Transform CreateNeighborIndicator(GridCell cell, bool positive)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = positive ? "SynergyIndicator" : "ConflictIndicator";
            indicator.transform.SetParent(cell.transform, false);
            indicator.transform.localScale = _neighborIndicatorScale;

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material template = positive ? _synergyIndicatorMaterial : _conflictIndicatorMaterial;
                if (template != null)
                {
                    renderer.sharedMaterial = new Material(template);
                }
            }

            return indicator.transform;
        }

        private Transform CreateDestroyedSlash(Transform parent, string name)
        {
            GameObject slash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slash.name = name;
            slash.transform.SetParent(parent, false);
            slash.transform.localScale = _destroyedIndicatorScale;
            Collider collider = slash.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = slash.GetComponent<Renderer>();
            if (renderer != null && _destroyedIndicatorMaterial != null)
            {
                renderer.sharedMaterial = new Material(_destroyedIndicatorMaterial);
            }

            return slash.transform;
        }

        private void UpdateIndicator(GridCell cell, Transform indicator)
        {
            if (indicator == null)
            {
                return;
            }

            float bobOffset = Mathf.Sin((Time.time * _indicatorBobSpeed) + ((cell.X + cell.Y + cell.PlotId) * 0.7f)) * _indicatorBobAmplitude;
            float topY = cell.transform.position.y;

            if (cell.Plant != null)
            {
                Renderer[] renderers = cell.Plant.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer != null && renderer.enabled)
                    {
                        topY = Mathf.Max(topY, renderer.bounds.max.y);
                    }
                }
            }

            indicator.position = new Vector3(
                cell.transform.position.x + _indicatorWorldOffset.x,
                topY + _indicatorWorldOffset.y + bobOffset,
                cell.transform.position.z + _indicatorWorldOffset.z);
            indicator.rotation = Quaternion.Euler(45f, (Time.time * 90f) % 360f, 45f);
        }

        private void UpdateWateringIndicator(GridCell cell, Transform indicator)
        {
            if (indicator == null)
            {
                return;
            }

            float bob = Mathf.Sin((Time.time * _stateIndicatorBobSpeed) + (cell.X * 0.6f)) * _stateIndicatorBobAmplitude;
            indicator.position = cell.transform.position + Vector3.up * (_stateIndicatorHeight + bob);
            indicator.rotation = Quaternion.Euler(0f, (Time.time * 50f) % 360f, 0f);
        }

        private void UpdateDestroyedIndicator(GridCell cell, Transform indicator)
        {
            if (indicator == null)
            {
                return;
            }

            float bob = Mathf.Sin((Time.time * (_stateIndicatorBobSpeed * 0.75f)) + (cell.Y * 0.5f)) * (_stateIndicatorBobAmplitude * 0.35f);
            indicator.position = cell.transform.position + Vector3.up * (_stateIndicatorHeight + 0.03f + bob);
        }

        private void UpdateNeighborIndicator(GridCell cell, Transform indicator, float relationScore)
        {
            if (indicator == null)
            {
                return;
            }

            float bob = Mathf.Sin((Time.time * _neighborIndicatorBobSpeed) + ((cell.X + cell.Y) * 0.7f)) * _neighborIndicatorBobAmplitude;
            float topY = cell.transform.position.y;
            if (cell.Plant != null)
            {
                Renderer[] renderers = cell.Plant.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer != null && renderer.enabled)
                    {
                        topY = Mathf.Max(topY, renderer.bounds.max.y);
                    }
                }
            }

            indicator.position = cell.transform.position + Vector3.up * (topY - cell.transform.position.y + _neighborIndicatorHeight + bob);
            float intensity = Mathf.Clamp01(Mathf.Abs(relationScore) * 1.2f);
            indicator.localScale = _neighborIndicatorScale * Mathf.Lerp(0.8f, 1.35f, intensity);
            indicator.rotation = Quaternion.Euler(0f, (Time.time * 65f) % 360f, 0f);

            Renderer rendererComponent = indicator.GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                bool positive = relationScore > 0f;
                Material material = rendererComponent.sharedMaterial;
                Color baseColor = positive ? _synergyIndicatorColor : _conflictIndicatorColor;
                ApplyMaterialColor(material, baseColor, baseColor * Mathf.Lerp(1.2f, 1.9f, intensity));
            }
        }

        private static void CleanupIndicators(Dictionary<GridCell, Transform> dictionary, HashSet<GridCell> activeCells)
        {
            if (dictionary.Count == 0)
            {
                return;
            }

            List<GridCell> cellsToRemove = null;
            foreach ((GridCell cell, Transform indicator) in dictionary)
            {
                if (activeCells.Contains(cell) && indicator != null)
                {
                    continue;
                }

                cellsToRemove ??= new List<GridCell>();
                cellsToRemove.Add(cell);

                if (indicator != null)
                {
                    Destroy(indicator.gameObject);
                }
            }

            if (cellsToRemove == null)
            {
                return;
            }

            foreach (GridCell cell in cellsToRemove)
            {
                dictionary.Remove(cell);
            }
        }

        private static Material CreateRuntimeMaterial(string materialName, Color baseColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new(shader)
            {
                name = materialName
            };

            ApplyMaterialColor(material, baseColor, baseColor * 1.3f);
            return material;
        }

        private static void ApplyMaterialColor(Material material, Color baseColor, Color emissionColor)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, baseColor);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, baseColor);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, emissionColor);
            }
        }
    }
}
