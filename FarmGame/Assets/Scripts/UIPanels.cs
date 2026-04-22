using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmSimulator.Core;
using FarmSimulator.Data;

namespace FarmSimulator.UI
{
    // ─────────────────────────────────────────────────────────────────────────
    // CellInfoPanel — shows plant data, mutations, soil params
    // ─────────────────────────────────────────────────────────────────────────
    public class CellInfoPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _plantNameLabel;
        [SerializeField] private TextMeshProUGUI _growthLabel;
        [SerializeField] private TextMeshProUGUI _yieldLabel;
        [SerializeField] private TextMeshProUGUI _valueLabel;
        [SerializeField] private TextMeshProUGUI _mutationsLabel;
        [SerializeField] private TextMeshProUGUI _soilLabel;
        [SerializeField] private Slider          _growthSlider;
        [SerializeField] private Button          _harvestButton;
        [SerializeField] private Button          _closeButton;

        [Header("Harvest")]
        [SerializeField] private EconomyManager _economy;

        private GridCell _cell;

        private void Awake()
        {
            _closeButton?.onClick.AddListener(Hide);
            _harvestButton?.onClick.AddListener(OnHarvestPressed);
            Hide();
        }

        public void Show(GridCell cell)
        {
            _cell = cell;
            _root?.SetActive(true);
            Refresh(cell);
        }

        public void Refresh(GridCell cell)
        {
            if (cell == null || _root == null || !_root.activeSelf) return;

            if (cell.IsEmpty)
            {
                _plantNameLabel?.SetText("Empty cell");
                _growthLabel?.SetText("");
                _yieldLabel?.SetText("");
                _valueLabel?.SetText("");
                _mutationsLabel?.SetText("");
                if (_growthSlider != null) _growthSlider.value = 0f;
                _harvestButton?.gameObject.SetActive(false);
            }
            else
            {
                var plant = cell.Plant;
                _plantNameLabel?.SetText(plant.Data.plantName);
                _growthLabel?.SetText($"Growth: {plant.Growth * 100:F0}%  [{plant.State}]");
                _yieldLabel?.SetText($"Yield: {plant.CalculateYield():F1}");
                _valueLabel?.SetText($"Value: {plant.CalculateValue():F0} g");

                if (_growthSlider != null) _growthSlider.value = plant.Growth;

                // Mutations
                var sb = new System.Text.StringBuilder("Mutations:\n");
                if (plant.Mutations.Count == 0) sb.Append("  none");
                foreach (var m in plant.Mutations)
                    sb.AppendLine($"  [{m.rarity}] {m.mutationName}");
                _mutationsLabel?.SetText(sb.ToString());

                // Soil
                _soilLabel?.SetText(
                    $"Soil — Fertility: {cell.Soil.fertility:P0}  " +
                    $"Moisture: {cell.Soil.moisture:P0}  " +
                    $"Infection: {cell.Soil.infection:P0}");

                _harvestButton?.gameObject.SetActive(plant.State == PlantState.Mature);
            }
        }

        public void Hide() => _root?.SetActive(false);

        private void OnHarvestPressed()
        {
            if (_economy != null && _cell != null)
                _economy.Sell(_cell);
            Hide();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EventNotification — toast-style banner for farm events
    // ─────────────────────────────────────────────────────────────────────────
    public class EventNotification : MonoBehaviour
    {
        [SerializeField] private GameObject      _root;
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _descLabel;
        [SerializeField] private Image           _icon;
        [SerializeField] private float           _displayDuration = 4f;

        private Coroutine _hideCoroutine;

        private void Awake() => _root?.SetActive(false);

        public void Show(ActiveEvent active)
        {
            if (_root == null) return;

            _titleLabel?.SetText(active.Data.eventName);
            _descLabel?.SetText(active.Data.description);
            if (_icon != null && active.Data.icon != null)
                _icon.sprite = active.Data.icon;

            _root.SetActive(true);

            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfter(_displayDuration));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _root?.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HarvestPanel — scrollable sale history
    // ─────────────────────────────────────────────────────────────────────────
    public class HarvestPanel : MonoBehaviour
    {
        [SerializeField] private GameObject      _rowPrefab;
        [SerializeField] private Transform       _container;
        [SerializeField] private TextMeshProUGUI _totalLabel;

        public void RefreshHistory(IReadOnlyList<SaleRecord> history)
        {
            foreach (Transform child in _container) Destroy(child.gameObject);

            float total = 0f;
            foreach (var record in history)
            {
                var row = Instantiate(_rowPrefab, _container);
                var label = row.GetComponentInChildren<TextMeshProUGUI>();
                label?.SetText(
                    $"{record.PlantName}  " +
                    $"x{record.MutationCount} mut [{record.HighestRarity}]  " +
                    $"→ {record.Value:F0} g");
                total += record.Value;
            }

            _totalLabel?.SetText($"Total earned: {total:F0} g");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CellUI — click handler on each grid cell world object
    // ─────────────────────────────────────────────────────────────────────────
    [RequireComponent(typeof(GridCell))]
    public class CellUI : MonoBehaviour
    {
        [SerializeField] private FarmUI _farmUI;

        private GridCell _cell;

        private void Awake() => _cell = GetComponent<GridCell>();

        private void OnMouseDown() => _farmUI?.OnCellSelected(_cell);
    }
}
