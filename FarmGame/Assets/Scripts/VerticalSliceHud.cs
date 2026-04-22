using System.Collections.Generic;
using System.Text;
using FarmSimulator.Core;
using FarmSimulator.Data;
using FarmSimulator.Player;
using FarmSimulator.Visual;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarmSimulator.UI
{
    public class VerticalSliceHud : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private EconomyManager _economy;
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private InteractionController _interaction;
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private GridManager _gridManager;

        [Header("Canvas HUD")]
        [SerializeField] private Canvas _hudCanvas;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _summaryText;
        [SerializeField] private Text _promptText;
        [SerializeField] private Text _eventText;
        [SerializeField] private Text _eventBannerText;
        [SerializeField] private Text _eventIconText;
        [SerializeField] private Text _selectionTitleText;
        [SerializeField] private Text _selectionBodyText;
        [SerializeField] private Text _historyText;
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private Text _inventoryTitleText;
        [SerializeField] private Text _inventoryBodyText;
        [SerializeField] private GameObject _tradePanel;
        [SerializeField] private Text _tradeTitleText;
        [SerializeField] private Text _tradeBodyText;
        [SerializeField] private Text _cameraButtonLabel;
        [SerializeField] private Button _cameraToggleButton;
        [SerializeField] private bool _useImmediateGuiFallback = true;

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private Vector2 _historyScroll;

        private void Awake()
        {
            if (_cameraController == null)
            {
                _cameraController = FindFirstObjectByType<CameraController>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }

            if (_hudCanvas != null)
            {
                _useImmediateGuiFallback = false;
            }
        }

        private void OnEnable()
        {
            if (_cameraToggleButton != null)
            {
                _cameraToggleButton.onClick.AddListener(ToggleCameraMode);
            }
        }

        private void OnDisable()
        {
            if (_cameraToggleButton != null)
            {
                _cameraToggleButton.onClick.RemoveListener(ToggleCameraMode);
            }
        }

        private void Update()
        {
            if (WasInventoryTogglePressed())
            {
                ToggleInventoryPanel();
            }

            if (HasCanvasHud())
            {
                UpdateCanvasHud();
            }
        }

        private void OnGUI()
        {
            if (!_useImmediateGuiFallback || HasCanvasHud())
            {
                return;
            }

            EnsureStyles();
            DrawHud();
            DrawSelection();
            DrawHistory();
        }

        private void UpdateCanvasHud()
        {
            if (_titleText != null)
            {
                _titleText.text = "Ферма мутаций";
            }

            if (_summaryText != null)
            {
                StringBuilder summary = new();
                summary.AppendLine($"Золото: {_economy.Gold:F0}");
                summary.AppendLine($"Заработано: {_economy.TotalEarned:F0}");
                if (_inventory != null)
                {
                    summary.AppendLine($"Инвентарь: {_inventory.OccupiedSlotsCount}/{_inventory.SlotCount}");
                    summary.AppendLine(_inventory.HasWateringCan ? "Инструмент: канистра" : "Инструмент: нет канистры");
                }
                summary.AppendLine();
                summary.AppendLine("Q / R: сменить культуру");
                summary.AppendLine("WASD: движение");
                summary.AppendLine("E: действие");
                summary.AppendLine("F: купить канистру");
                summary.AppendLine("I: инвентарь");
                summary.AppendLine("ПКМ + мышь: осмотреться");
                _summaryText.text = summary.ToString();
            }

            if (_promptText != null)
            {
                _promptText.text = _interaction.GetInteractionPrompt();
            }

            if (_eventText != null || _eventBannerText != null || _eventIconText != null)
            {
                if (_eventSystem.ActiveEvents.Count > 0)
                {
                    ActiveEvent primaryEvent = _eventSystem.ActiveEvents[0];
                    string icon = GetEventIcon(primaryEvent.Data);

                    if (_eventBannerText != null)
                    {
                        _eventBannerText.text = $"{primaryEvent.Data.eventName} • {primaryEvent.TimeRemaining:F0}с";
                    }

                    if (_eventIconText != null)
                    {
                        _eventIconText.text = icon;
                        _eventIconText.color = GetEventColor(primaryEvent.Data);
                    }

                    if (_eventText != null)
                    {
                        StringBuilder eventsBuilder = new("Активные события");
                        foreach (ActiveEvent active in _eventSystem.ActiveEvents)
                        {
                            eventsBuilder.AppendLine();
                            eventsBuilder.Append($"• {active.Data.eventName} ({active.TimeRemaining:F0}с)");
                        }

                        _eventText.text = eventsBuilder.ToString();
                    }
                }
                else
                {
                    if (_eventBannerText != null)
                    {
                        _eventBannerText.text = $"Следующее событие через {_eventSystem.NextEventTimer:F0}с";
                    }

                    if (_eventIconText != null)
                    {
                        _eventIconText.text = "○";
                        _eventIconText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
                    }

                    if (_eventText != null)
                    {
                        _eventText.text = $"Следующее событие через {_eventSystem.NextEventTimer:F0}с";
                    }
                }
            }

            if (_selectionTitleText != null)
            {
                _selectionTitleText.text = _interaction.CurrentZone != null ? "Точка взаимодействия" : "Выбранная ячейка";
            }

            if (_selectionBodyText != null)
            {
                _selectionBodyText.text = BuildSelectionText();
            }

            if (_historyText != null)
            {
                _historyText.text = BuildHistoryText();
            }

            if (_inventoryTitleText != null && _inventory != null)
            {
                _inventoryTitleText.text = $"Инвентарь ({_inventory.OccupiedSlotsCount}/{_inventory.SlotCount})";
            }

            if (_inventoryBodyText != null)
            {
                _inventoryBodyText.text = BuildInventoryText();
            }

            if (_tradePanel != null)
            {
                bool showTrade = _interaction != null &&
                    (_interaction.CurrentZone != null || (_interaction.SelectedPlant != null && (_interaction.CurrentCell != null || _interaction.NearestCell != null)));
                _tradePanel.SetActive(showTrade);
            }

            if (_tradeTitleText != null)
            {
                _tradeTitleText.text = BuildTradeTitle();
            }

            if (_tradeBodyText != null)
            {
                _tradeBodyText.text = BuildTradeText();
            }

            if (_cameraButtonLabel != null)
            {
                _cameraButtonLabel.text = _cameraController != null && _cameraController.IsFirstPerson
                    ? "Камера: 1-е лицо"
                    : "Камера: 3-е лицо";
            }
        }

        private string BuildSelectionText()
        {
            if (_interaction.CurrentZone != null)
            {
                return BuildZoneText(_interaction.CurrentZone, _interaction.SelectedPlant);
            }

            GridCell cell = _interaction.CurrentCell;
            if (cell == null)
            {
                return "Нет точки взаимодействия в радиусе действия";
            }

            if (cell.IsEmpty)
            {
                StringBuilder builder = new();
                builder.AppendLine($"Грядка {cell.PlotId + 1} / [{cell.X},{cell.Y}]");
                builder.AppendLine(GetCellStatusLine(cell));
                builder.AppendLine($"Почва: плодородие {cell.Soil.fertility:P0}, влажность {cell.Soil.moisture:P0}");
                builder.AppendLine();
                if (_interaction.SelectedPlant != null)
                {
                    builder.AppendLine(BuildPlantDescription(_interaction.SelectedPlant));
                    builder.AppendLine(BuildPredictedNeighborImpact(cell, _interaction.SelectedPlant));
                    builder.AppendLine();
                    if (_inventory != null)
                    {
                    builder.AppendLine($"В рюкзаке семян: {_inventory.GetSeedCount(_interaction.SelectedPlant)}");
                }
            }
            return builder.ToString().TrimEnd();
        }

            PlantInstance instance = cell.Plant;
            StringBuilder selection = new();
            selection.AppendLine($"Грядка {cell.PlotId + 1} / [{cell.X},{cell.Y}]");
            selection.AppendLine(GetCellStatusLine(cell));
            selection.AppendLine(instance.Data.plantName);
            selection.AppendLine(instance.Data.description);
            selection.AppendLine($"Состояние: {TranslateState(instance.State)}");
            selection.AppendLine($"Рост: {instance.Growth * 100f:F0}%");
            selection.AppendLine($"Урожай: {instance.CalculateYield():F1}");
            selection.AppendLine($"Базовая цена единицы: {instance.Data.baseValue:F0}g");
            selection.AppendLine($"Множитель мутаций: x{instance.CalculateMutationValueMultiplier():F2}");
            selection.AppendLine($"Стоимость партии: {instance.CalculateValue():F0}g");
            selection.AppendLine(BuildSaleAdvice(instance.CalculateValue(), instance.Data.baseValue * Mathf.Max(1f, instance.CalculateYield())));
            selection.AppendLine($"Вклад цены: {instance.BuildValueBreakdown()}");
            selection.AppendLine($"Синергия: {FormatGroups(instance.Data.synergisticGroups)}");
            selection.AppendLine($"Конфликт: {FormatGroups(instance.Data.conflictingGroups)}");
            selection.AppendLine(BuildNeighborImpactSummary(instance));
            selection.AppendLine($"Мутации: {FormatMutations(instance.Mutations)}");
            if (instance.HasBurningMutation)
            {
                selection.AppendLine($"Горение: цена тает, текущий фактор x{instance.BurningValueFactor:F2}");
            }
            selection.Append($"Почва: плодородие {cell.Soil.fertility:P0}, влажность {cell.Soil.moisture:P0}, заражение {cell.Soil.infection:P0}");
            return selection.ToString();
        }

        private string BuildZoneText(WorldInteractionZone zone, PlantData plant)
        {
            StringBuilder builder = new();
            builder.AppendLine(zone.DisplayName);
            if (!string.IsNullOrWhiteSpace(zone.Description))
            {
                builder.AppendLine(zone.Description);
                builder.AppendLine();
            }

            if (plant != null)
            {
                builder.AppendLine(BuildPlantDescription(plant));
                if (_inventory != null)
                {
                    builder.AppendLine();
                    builder.AppendLine($"Семян в инвентаре: {_inventory.GetSeedCount(plant)}");
                    builder.AppendLine($"Урожая в инвентаре: {_inventory.GetCropCount(plant)}");
                    if (zone.ZoneType == Player.WorldInteractionZoneType.Shop)
                    {
                        builder.AppendLine(_inventory.HasWateringCan
                            ? "Канистра уже куплена"
                            : "F: купить канистру для полива");
                    }
                }
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildHistoryText()
        {
            IReadOnlyList<SaleRecord> history = _economy.History;
            StringBuilder builder = new("Последние продажи");
            if (history.Count == 0)
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.Append("Пока ничего не продано");
                return builder.ToString();
            }

            for (int i = history.Count - 1; i >= 0 && i >= history.Count - 8; i--)
            {
                SaleRecord record = history[i];
                builder.AppendLine();
                builder.Append($"• {record.PlantName}: {record.Value:F0}g");
            }

            return builder.ToString();
        }

        private string BuildInventoryText()
        {
            if (_inventory == null)
            {
                return "Инвентарь недоступен";
            }

            StringBuilder builder = new();
            builder.AppendLine(_inventory.HasWateringCan ? "Инструмент: канистра" : "Инструмент: не куплен");
            builder.AppendLine($"Свободно слотов: {_inventory.SlotCount - _inventory.OccupiedSlotsCount}");
            builder.AppendLine();

            int listed = 0;
            IReadOnlyList<InventorySlotData> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                listed++;
                string itemType = slot.kind == InventoryItemKind.Seed ? "Семена" : "Урожай";
                builder.Append($"[{i + 1:00}] {itemType}: {slot.DisplayName} x{slot.quantity}");

                if (slot.kind == InventoryItemKind.Crop)
                {
                    builder.Append($" • {slot.saleValue:F0}g");
                    builder.Append($" • x{slot.valueMultiplier:F2}");
                    if (slot.isBurning)
                    {
                        builder.Append(" • горит");
                    }
                    builder.Append($" • {BuildSaleAdvice(slot.saleValue, slot.plant != null ? slot.plant.baseValue * slot.plant.baseYield : 0f)}");
                    if (!string.IsNullOrWhiteSpace(slot.mutationSummary))
                    {
                        builder.Append($" • {slot.mutationSummary}");
                    }
                }

                builder.AppendLine();

                if (listed >= 18)
                {
                    builder.AppendLine();
                    builder.Append("...");
                    break;
                }
            }

            if (listed == 0)
            {
                builder.Append("Пока пусто");
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildTradeTitle()
        {
            if (_interaction == null || _interaction.CurrentZone == null)
            {
                return "Подбор культуры";
            }

            return _interaction.CurrentZone.ZoneType switch
            {
                WorldInteractionZoneType.Shop => "Магазин семян",
                WorldInteractionZoneType.SellStall => "Лавка продажи",
                _ => _interaction.CurrentZone.DisplayName
            };
        }

        private string BuildTradeText()
        {
            if (_interaction == null || _interaction.CurrentZone == null)
            {
                if (_interaction == null || _interaction.SelectedPlant == null)
                {
                    return "Выберите культуру и подойдите к грядке";
                }

                GridCell previewCell = _interaction.CurrentCell ?? _interaction.NearestCell;
                if (previewCell == null)
                {
                    return "Подойдите к грядке, чтобы увидеть прогноз посадки";
                }

                StringBuilder preview = new();
                preview.AppendLine(_interaction.SelectedPlant.plantName);
                preview.AppendLine($"Группа: {TranslateGroup(_interaction.SelectedPlant.group)}");
                preview.AppendLine(BuildPredictedNeighborImpact(previewCell, _interaction.SelectedPlant));
                preview.Append(BuildPlacementVerdict(previewCell, _interaction.SelectedPlant));
                return preview.ToString();
            }

            WorldInteractionZone zone = _interaction.CurrentZone;
            PlantData selected = _interaction.SelectedPlant;
            StringBuilder builder = new();
            builder.AppendLine(zone.DisplayName);

            if (!string.IsNullOrWhiteSpace(zone.Description))
            {
                builder.AppendLine(zone.Description);
            }

            if (selected == null)
            {
                builder.AppendLine();
                builder.Append("Нет выбранной культуры");
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine(selected.plantName);
            builder.AppendLine($"Цена семени: {selected.seedCost:F0}g");
            builder.AppendLine($"Базовая стоимость урожая: {selected.baseValue:F0}g");
            builder.AppendLine($"Группа: {TranslateGroup(selected.group)}");
            builder.AppendLine($"Синергия: {FormatGroups(selected.synergisticGroups)}");
            builder.AppendLine($"Конфликт: {FormatGroups(selected.conflictingGroups)}");
            builder.AppendLine($"Потенциал мутаций: {BuildEventMutationValueGuide(selected)}");

            if (_inventory != null)
            {
                builder.AppendLine($"Семян в инвентаре: {_inventory.GetSeedCount(selected)}");
                builder.AppendLine($"Урожая в инвентаре: {_inventory.GetCropCount(selected)}");
                InventorySlotData bestBatch = FindBestCropSlot(selected);
                if (bestBatch != null)
                {
                    builder.AppendLine($"Лучшая партия: {bestBatch.saleValue:F0}g • x{bestBatch.valueMultiplier:F2}");
                    builder.AppendLine(BuildSaleAdvice(bestBatch.saleValue, selected.baseValue * selected.baseYield));
                }
            }

            builder.AppendLine();
            if (zone.ZoneType == WorldInteractionZoneType.Shop)
            {
                builder.AppendLine("E: купить 1 семя выбранной культуры");
                builder.Append(_inventory != null && _inventory.HasWateringCan
                    ? "Канистра уже куплена"
                    : "F: купить канистру для полива (60g)");
            }
            else
            {
                builder.Append("E: продать 1 партию выбранной культуры");
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetCellStatusLine(GridCell cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            if (cell.IsDestroyed)
            {
                return $"Статус: разрушена, ремонт {GridCell.RepairCost:F0}g";
            }

            if (cell.RequiresWatering)
            {
                return "Статус: пересохла, требуется полив";
            }

            return "Статус: стабильна";
        }

        private string BuildPlantDescription(PlantData plant)
        {
            if (plant == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            builder.AppendLine(plant.plantName);
            if (!string.IsNullOrWhiteSpace(plant.description))
            {
                builder.AppendLine(plant.description);
            }
            builder.AppendLine($"Группа растения: {TranslateGroup(plant.group)}");
            builder.AppendLine($"Семя: {plant.seedCost:F0}g");
            builder.AppendLine($"База: урожай {plant.baseYield:F0}, стоимость {plant.baseValue:F0}g");
            builder.AppendLine($"Синергия: {FormatGroups(plant.synergisticGroups)}");
            builder.AppendLine($"Конфликт: {FormatGroups(plant.conflictingGroups)}");
            builder.AppendLine($"Ценность мутаций: {BuildEventMutationValueGuide(plant)}");
            builder.Append($"Событийные мутации: {BuildEventMutationHintsForPlant(plant)}");
            return builder.ToString();
        }

        private string BuildEventMutationHintsForPlant(PlantData plant)
        {
            if (plant == null || _eventSystem == null || _eventSystem.AllEvents == null)
            {
                return "нет данных";
            }

            StringBuilder builder = new();
            bool hasAny = false;
            IReadOnlyList<EventData> allEvents = _eventSystem.AllEvents;
            for (int i = 0; i < allEvents.Count; i++)
            {
                EventData eventData = allEvents[i];
                if (eventData == null || eventData.eventMutation == null)
                {
                    continue;
                }

                if (!eventData.affectsAllGroups)
                {
                    bool affectsGroup = false;
                    for (int groupIndex = 0; groupIndex < eventData.affectedGroups.Length; groupIndex++)
                    {
                        if (eventData.affectedGroups[groupIndex] == plant.group)
                        {
                            affectsGroup = true;
                            break;
                        }
                    }

                    if (!affectsGroup)
                    {
                        continue;
                    }
                }

                if (hasAny)
                {
                    builder.Append("; ");
                }

                builder.Append($"{eventData.eventMutation.mutationName} <- {eventData.eventName}");
                hasAny = true;
            }

            return hasAny ? builder.ToString() : "нет";
        }

        private string BuildEventMutationValueGuide(PlantData plant)
        {
            if (plant == null || _eventSystem == null || _eventSystem.AllEvents == null)
            {
                return "нет данных";
            }

            StringBuilder builder = new();
            bool hasAny = false;
            IReadOnlyList<EventData> allEvents = _eventSystem.AllEvents;
            for (int i = 0; i < allEvents.Count; i++)
            {
                EventData eventData = allEvents[i];
                MutationData mutation = eventData != null ? eventData.eventMutation : null;
                if (mutation == null)
                {
                    continue;
                }

                if (!EventAffectsPlantGroup(eventData, plant.group))
                {
                    continue;
                }

                if (hasAny)
                {
                    builder.Append("; ");
                }

                float percent = (GetEffectiveMutationValueFactor(mutation) - 1f) * 100f;
                string sign = percent >= 0f ? "+" : string.Empty;
                builder.Append($"{mutation.mutationName} {sign}{percent:F0}%");
                hasAny = true;
            }

            return hasAny ? builder.ToString() : "нет";
        }

        private string FormatMutations(IReadOnlyList<MutationData> mutations)
        {
            if (mutations == null || mutations.Count == 0)
            {
                return "нет";
            }

            StringBuilder builder = new();
            for (int i = 0; i < mutations.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(FormatMutationLabel(mutations[i]));
            }

            return builder.ToString();
        }

        private string FormatMutationLabel(MutationData mutation)
        {
            if (mutation == null)
            {
                return "неизвестно";
            }

            float percent = (GetEffectiveMutationValueFactor(mutation) - 1f) * 100f;
            string valueHint = Mathf.Abs(percent) > 0.01f
                ? $" {((percent >= 0f) ? "+" : string.Empty)}{percent:F0}%"
                : string.Empty;

            if (mutation.eventOnly && !string.IsNullOrWhiteSpace(mutation.sourceEventName))
            {
                return $"{mutation.mutationName}{valueHint} [{mutation.sourceEventName}]";
            }

            return $"{mutation.mutationName}{valueHint}";
        }

        private float GetEffectiveMutationValueFactor(MutationData mutation)
        {
            if (mutation == null)
            {
                return 1f;
            }

            float rarityFactor = mutation.rarity switch
            {
                MutationRarity.Common => _config != null ? _config.commonMutationValueMult : 1.3f,
                MutationRarity.Rare => _config != null ? _config.rareMutationValueMult : 2.5f,
                MutationRarity.Unique => _config != null ? _config.uniqueMutationValueMult : 5f,
                _ => 1f
            };

            return mutation.valueMultiplier * rarityFactor;
        }

        private InventorySlotData FindBestCropSlot(PlantData plant)
        {
            if (_inventory == null || plant == null)
            {
                return null;
            }

            InventorySlotData best = null;
            IReadOnlyList<InventorySlotData> slots = _inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null || slot.IsEmpty || slot.kind != InventoryItemKind.Crop || slot.plant != plant)
                {
                    continue;
                }

                if (best == null || slot.saleValue > best.saleValue)
                {
                    best = slot;
                }
            }

            return best;
        }

        private static string BuildSaleAdvice(float saleValue, float baselineValue)
        {
            if (baselineValue <= 0.01f)
            {
                return "оценка цены недоступна";
            }

            float ratio = saleValue / baselineValue;
            if (ratio >= 1.2f)
            {
                return "выгодная партия";
            }

            if (ratio >= 0.95f)
            {
                return "цена близка к базовой";
            }

            return "уценка из-за слабых мутаций";
        }

        private static bool EventAffectsPlantGroup(EventData eventData, PlantGroup group)
        {
            if (eventData == null)
            {
                return false;
            }

            if (eventData.affectsAllGroups)
            {
                return true;
            }

            if (eventData.affectedGroups == null)
            {
                return false;
            }

            for (int i = 0; i < eventData.affectedGroups.Length; i++)
            {
                if (eventData.affectedGroups[i] == group)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatGroups(PlantGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
            {
                return "нет";
            }

            StringBuilder builder = new();
            for (int i = 0; i < groups.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(TranslateGroup(groups[i]));
            }

            return builder.ToString();
        }

        private void ToggleCameraMode()
        {
            _cameraController?.ToggleMode();
        }

        private void ToggleInventoryPanel()
        {
            if (_inventoryPanel == null)
            {
                return;
            }

            _inventoryPanel.SetActive(!_inventoryPanel.activeSelf);
        }

        private static bool WasInventoryTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.iKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private bool HasCanvasHud()
        {
            return _hudCanvas != null && _summaryText != null && _selectionBodyText != null && _historyText != null;
        }

        private void DrawHud()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 380f, 220f), _panelStyle);
            GUILayout.Label("Ferma mutatsiy", _titleStyle);
            GUILayout.Label($"Zoloto: {_economy.Gold:F0}", _labelStyle);
            if (_inventory != null)
            {
                GUILayout.Label($"Inventar: {_inventory.OccupiedSlotsCount}/{_inventory.SlotCount}", _labelStyle);
            }
            GUILayout.Space(6f);
            GUILayout.Label("Q / R: smenit kul'turu", _labelStyle);
            GUILayout.Label("WASD: dvizhenie", _labelStyle);
            GUILayout.Label("E: deystvie", _labelStyle);
            GUILayout.Label("PKM + mysh: osmotret'sya", _labelStyle);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 240f, Screen.height - 86f, 480f, 48f), _panelStyle);
            GUILayout.Label(_interaction.GetInteractionPrompt(), _titleStyle);
            GUILayout.EndArea();
        }

        private void DrawSelection()
        {
            GUILayout.BeginArea(new Rect(16f, 246f, 460f, 300f), _panelStyle);
            GUILayout.Label("Vybrannaya tochka", _titleStyle);
            GUILayout.Label(BuildSelectionText(), _labelStyle);
            GUILayout.EndArea();
        }

        private void DrawHistory()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 360f, 16f, 340f, 280f), _panelStyle);
            GUILayout.Label("Poslednie prodazhi", _titleStyle);
            _historyScroll = GUILayout.BeginScrollView(_historyScroll, false, true);
            GUILayout.Label(BuildHistoryText(), _labelStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string TranslateState(PlantState state)
        {
            return state switch
            {
                PlantState.Growing => "Растет",
                PlantState.Mature => "Созрело",
                PlantState.Harvested => "Собрано",
                PlantState.Dead => "Погибло",
                _ => state.ToString()
            };
        }

        private static string TranslateGroup(PlantGroup group)
        {
            return group switch
            {
                PlantGroup.Vegetables => "Овощи",
                PlantGroup.Fruits => "Фрукты",
                PlantGroup.Grains => "Злаки",
                PlantGroup.Exotic => "Экзотические",
                _ => group.ToString()
            };
        }

        private static string BuildNeighborImpactSummary(PlantInstance instance)
        {
            if (instance == null)
            {
                return "Соседство: нет данных";
            }

            return $"Соседство: рост {FormatSignedPercent(instance.GrowthRateModifier)}, урожай {FormatSignedPercent(instance.YieldModifier)}, цена {FormatSignedPercent(instance.ValueModifier)}";
        }

        private static string FormatSignedPercent(float value)
        {
            float percent = value * 100f;
            string sign = percent >= 0f ? "+" : string.Empty;
            return $"{sign}{percent:F0}%";
        }

        private string BuildPredictedNeighborImpact(GridCell cell, PlantData plant)
        {
            if (_gridManager == null || cell == null || plant == null)
            {
                return "Прогноз соседства: нет данных";
            }

            var (growthBonus, yieldBonus, valueBonus) = _gridManager.CalculatePredictedNeighborBonus(cell, plant);
            return $"Прогноз соседства: рост {FormatSignedPercent(growthBonus)}, урожай {FormatSignedPercent(yieldBonus)}, цена {FormatSignedPercent(valueBonus)}";
        }

        private string BuildPlacementVerdict(GridCell cell, PlantData plant)
        {
            if (cell == null)
            {
                return "Оценка места: нет данных";
            }

            if (cell.IsDestroyed)
            {
                return "Оценка места: сначала восстановите грядку";
            }

            if (cell.RequiresWatering)
            {
                return "Оценка места: сначала полейте грядку";
            }

            if (!cell.IsEmpty)
            {
                return $"Оценка места: клетка занята {cell.Plant.Data.plantName}";
            }

            if (_gridManager == null || plant == null)
            {
                return "Оценка места: нет данных";
            }

            var (growthBonus, yieldBonus, valueBonus) = _gridManager.CalculatePredictedNeighborBonus(cell, plant);
            float total = growthBonus + yieldBonus + valueBonus;
            if (total >= 0.35f)
            {
                return "Оценка места: очень выгодная посадка";
            }

            if (total >= 0.1f)
            {
                return "Оценка места: место дает заметную синергию";
            }

            if (total <= -0.45f)
            {
                return "Оценка места: жесткий конфликт, сажать невыгодно";
            }

            if (total <= -0.15f)
            {
                return "Оценка места: есть конфликт с соседями";
            }

            return "Оценка места: нейтральное";
        }

        private static string GetEventIcon(EventData eventData)
        {
            if (eventData == null)
            {
                return "○";
            }

            return eventData.eventName switch
            {
                "Вьюга" => "❄",
                "Ливень" => "☔",
                "Засуха" => "☀",
                "Полная луна" => "☾",
                "Гроза" => "⚡",
                "Мороз" => "✶",
                "Солнцестояние" => "✹",
                _ => eventData.isPositive ? "✦" : "✕"
            };
        }

        private static Color GetEventColor(EventData eventData)
        {
            if (eventData == null)
            {
                return Color.white;
            }

            return eventData.isPositive
                ? new Color(1f, 0.86f, 0.34f, 1f)
                : new Color(0.65f, 0.83f, 1f, 1f);
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 14, 14),
                fontSize = 15
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                richText = true,
                wordWrap = true
            };

            _titleStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
