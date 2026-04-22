using UnityEngine;
using FarmSimulator.Core;
using FarmSimulator.Player;
using FarmSimulator.UI;
using FarmSimulator.Visual;
using FarmSimulator.Data;

namespace FarmSimulator
{
    /// <summary>
    /// Scene entry point.
    /// Place on a root GameObject "GameBootstrap".
    /// Assign all references in the Inspector.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private BalanceConfig   _config;
        [SerializeField] private GridManager     _gridManager;
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private EconomyManager  _economy;
        [SerializeField] private MutationSystem  _mutationSystem;
        [SerializeField] private FarmSaveSystem  _saveSystem;
        [SerializeField] private InventoryManager _inventory;

        [Header("Presentation")]
        [SerializeField] private VFXManager      _vfxManager;
        [SerializeField] private CameraController _camera;
        [SerializeField] private VerticalSliceUI _runtimeUI;

        [Header("Gameplay")]
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private InteractionController _interactionController;
        [SerializeField] private PlantData[] _startingPlants;

        private void Start()
        {
            if (GetComponent<FullscreenToggleHotkey>() == null)
            {
                gameObject.AddComponent<FullscreenToggleHotkey>();
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 75;
            // Wire event system → VFX + camera
            _eventSystem.OnEventStarted += active =>
            {
                _vfxManager?.PlayEventVFX(active);
                _camera?.FocusOnEvent(active, _gridManager.transform.position);
                _saveSystem?.Save();
            };

            _eventSystem.OnEventEnded += active =>
            {
                _vfxManager?.StopEventVFX(active);
                _saveSystem?.Save();
            };

            // Wire mutation system → VFX + camera
            _mutationSystem.OnMutationTriggered += (cell, mutation) =>
            {
                _vfxManager?.PlayMutationVFX(cell, mutation);
                _camera?.FocusOnCell(cell);
            };

            // Wire economy
            _economy.OnSale += record =>
                Debug.Log($"[Economy] Sold {record.PlantName} for {record.Value:F0}g  " +
                          $"({record.MutationCount} mutations, highest: {record.HighestRarity})");

            if (_playerController != null && _config != null)
            {
                _camera?.ConfigureTarget(_playerController.transform);
                _playerController.Configure(_camera != null ? _camera.CameraPivot : null, _config.playerMoveSpeed);
                _inventory?.Configure(_config.inventorySlotCount);
            }

            if (_interactionController != null)
            {
                _interactionController.OnCellInteracted += _ => _saveSystem?.Save();
            }

            if (_saveSystem != null && !_saveSystem.Load())
            {
                SeedOpeningSetup();
            }
        }

        private void SeedOpeningSetup()
        {
            if (_startingPlants == null || _startingPlants.Length == 0)
            {
                return;
            }

            _gridManager.RecalcAllNeighborBonuses();
        }

        private void OnApplicationQuit()
        {
            _saveSystem?.Save();
        }
    }
}
