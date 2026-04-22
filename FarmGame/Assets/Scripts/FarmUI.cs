using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmSimulator.Core;
using FarmSimulator.Visual;
using FarmSimulator.Data;

namespace FarmSimulator.UI
{
    /// <summary>
    /// Top-level UI controller.
    /// Wires events from core systems to visual feedback.
    /// </summary>
    public class FarmUI : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private GridManager      _grid;
        [SerializeField] private FarmEventSystem  _eventSystem;
        [SerializeField] private EconomyManager   _economy;
        [SerializeField] private MutationSystem   _mutationSystem;

        [Header("Visual References")]
        [SerializeField] private VFXManager       _vfxManager;
        [SerializeField] private CameraController _camera;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _goldLabel;
        [SerializeField] private TextMeshProUGUI _totalEarnedLabel;

        [Header("Event Notification")]
        [SerializeField] private EventNotification _eventNotification;

        [Header("Cell Panel")]
        [SerializeField] private CellInfoPanel _cellInfoPanel;

        [Header("Harvest Panel")]
        [SerializeField] private HarvestPanel _harvestPanel;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            _economy.OnGoldChanged      += UpdateGoldHUD;
            _eventSystem.OnEventStarted += HandleEventStarted;
            _eventSystem.OnEventEnded   += HandleEventEnded;
            _mutationSystem.OnMutationTriggered += HandleMutation;
        }

        private void OnDisable()
        {
            _economy.OnGoldChanged      -= UpdateGoldHUD;
            _eventSystem.OnEventStarted -= HandleEventStarted;
            _eventSystem.OnEventEnded   -= HandleEventEnded;
            _mutationSystem.OnMutationTriggered -= HandleMutation;
        }

        private void Start() => UpdateGoldHUD(_economy.Gold);

        // ── HUD ───────────────────────────────────────────────────────────────
        private void UpdateGoldHUD(float gold)
        {
            if (_goldLabel        != null) _goldLabel.text        = $"Gold: {gold:F0}";
            if (_totalEarnedLabel != null) _totalEarnedLabel.text = $"Total: {_economy.TotalEarned:F0}";
        }

        // ── Events ────────────────────────────────────────────────────────────
        private void HandleEventStarted(ActiveEvent active)
        {
            _eventNotification?.Show(active);
            _vfxManager?.PlayEventVFX(active);
            _camera?.FocusOnEvent(active, _grid.transform.position);
        }

        private void HandleEventEnded(ActiveEvent active)
        {
            _vfxManager?.StopEventVFX(active);
        }

        private void HandleMutation(GridCell cell, MutationData mutation)
        {
            _vfxManager?.PlayMutationVFX(cell, mutation);
            _camera?.FocusOnCell(cell);
            _cellInfoPanel?.Refresh(cell);
        }

        // ── Cell selection (called by CellUI click) ───────────────────────────
        public void OnCellSelected(GridCell cell)
        {
            _cellInfoPanel?.Show(cell);
            _camera?.FocusOnCell(cell);
        }

        // ── Button callbacks ─────────────────────────────────────────────────
        public void OnSellAllPressed()
        {
            _economy.SellAll(_grid);
            _harvestPanel?.RefreshHistory(_economy.History);
        }
    }
}
