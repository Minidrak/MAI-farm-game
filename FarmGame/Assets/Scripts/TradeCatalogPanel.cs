using System.Collections.Generic;
using System.Text;
using FarmSimulator.Core;
using FarmSimulator.Data;
using FarmSimulator.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FarmSimulator.UI
{
    public class TradeCatalogPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionController _interaction;
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private EconomyManager _economy;
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private BalanceConfig _config;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private RectTransform _cardGridRoot;
        [SerializeField] private Text _detailsText;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new(0.33f, 0.25f, 0.18f, 0.98f);
        [SerializeField] private Color _selectedColor = new(0.74f, 0.56f, 0.25f, 1f);
        [SerializeField] private Color _shopColor = new(0.3f, 0.44f, 0.24f, 1f);
        [SerializeField] private Color _sellColor = new(0.46f, 0.28f, 0.2f, 1f);
        [SerializeField] private Color _previewColor = new(0.29f, 0.31f, 0.42f, 1f);

        private readonly List<Button> _buttons = new();
        private readonly List<Image> _images = new();
        private readonly List<Text> _texts = new();

        private void Awake()
        {
            if (_eventSystem == null)
            {
                _eventSystem = FindFirstObjectByType<FarmEventSystem>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }

            EnsureCards();
            Refresh();
        }

        private void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= Refresh;
            }
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_interaction == null || _cardGridRoot == null)
            {
                return;
            }

            EnsureCards();
            IReadOnlyList<PlantData> plants = _interaction.AvailablePlants;
            WorldInteractionZone zone = _interaction.CurrentZone;
            PlantData selected = _interaction.SelectedPlant;
            bool isShop = zone != null && zone.ZoneType == WorldInteractionZoneType.Shop;
            bool isSell = zone != null && zone.ZoneType == WorldInteractionZoneType.SellStall;
            bool previewMode = zone == null && selected != null;

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool active = plants != null && i < plants.Count && plants[i] != null;
                _buttons[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                PlantData plant = plants[i];
                int seedCount = _inventory != null ? _inventory.GetSeedCount(plant) : 0;
                int cropCount = _inventory != null ? _inventory.GetCropCount(plant) : 0;
                string actionLabel = isSell ? "Продажа" : isShop ? "Семя" : "План";
                float actionValue = isSell ? plant.baseValue : plant.seedCost;

                _texts[i].text =
                    $"{plant.plantName}\n" +
                    $"{actionLabel}: {actionValue:F0}g\n" +
                    $"С:{seedCount}  У:{cropCount}";

                bool selectedCard = selected == plant;
                if (selectedCard)
                {
                    _images[i].color = _selectedColor;
                }
                else if (isShop)
                {
                    _images[i].color = _shopColor;
                }
                else if (isSell)
                {
                    _images[i].color = _sellColor;
                }
                else if (previewMode)
                {
                    _images[i].color = _previewColor;
                }
                else
                {
                    _images[i].color = _normalColor;
                }
            }

            if (_detailsText != null)
            {
                _detailsText.text = BuildDetails(zone, selected);
            }
        }

        private void EnsureCards()
        {
            if (_interaction == null || _cardGridRoot == null)
            {
                return;
            }

            IReadOnlyList<PlantData> plants = _interaction.AvailablePlants;
            int targetCount = plants != null ? plants.Count : 0;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            while (_buttons.Count < targetCount)
            {
                int index = _buttons.Count;
                GameObject card = new($"TradeCard_{index + 1:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                card.transform.SetParent(_cardGridRoot, false);

                Image image = card.GetComponent<Image>();
                image.color = _normalColor;

                Button button = card.GetComponent<Button>();
                int capturedIndex = index;
                button.onClick.AddListener(() => _interaction.SetSelectedPlant(capturedIndex));

                GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(card.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 6f);
                labelRect.offsetMax = new Vector2(-6f, -6f);

                Text label = labelObject.GetComponent<Text>();
                label.font = font;
                label.fontSize = 14;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.color = Color.white;

                _buttons.Add(button);
                _images.Add(image);
                _texts.Add(label);
            }
        }

        private string BuildDetails(WorldInteractionZone zone, PlantData selected)
        {
            if (zone == null)
            {
                return BuildPlantingPreview(selected);
            }

            if (selected == null)
            {
                return "Нет выбранной культуры";
            }

            int seedCount = _inventory != null ? _inventory.GetSeedCount(selected) : 0;
            int cropCount = _inventory != null ? _inventory.GetCropCount(selected) : 0;
            InventorySlotData bestCrop = FindBestCropSlot(selected);

            return
                $"{selected.plantName}\n" +
                $"{selected.description}\n\n" +
                $"Золото: {(_economy != null ? _economy.Gold : 0f):F0}\n" +
                $"Группа: {TranslateGroup(selected.group)}\n" +
                $"Семя: {selected.seedCost:F0}g\n" +
                $"База урожая: {selected.baseValue:F0}g\n" +
                $"Семян в рюкзаке: {seedCount}\n" +
                $"Урожая в рюкзаке: {cropCount}\n" +
                (bestCrop != null ? $"Лучшая партия: {bestCrop.saleValue:F0}g • x{bestCrop.valueMultiplier:F2}\n{BuildSaleAdvice(bestCrop.saleValue, selected.baseValue * selected.baseYield)}\n" : string.Empty) +
                BuildCellForecastBlock(selected) +
                $"Синергия: {FormatGroups(selected.synergisticGroups)}\n" +
                $"Конфликт: {FormatGroups(selected.conflictingGroups)}\n" +
                $"Мутации и цена: {BuildEventMutationValueGuide(selected)}\n\n" +
                (zone.ZoneType == WorldInteractionZoneType.Shop
                    ? (_inventory != null && _inventory.HasWateringCan
                        ? "E: купить семя\nКанистра уже куплена"
                        : "E: купить семя\nF: купить канистру")
                    : "E: продать 1 партию");
        }

        private string BuildPlantingPreview(PlantData selected)
        {
            if (selected == null)
            {
                return "Выберите культуру для посадки";
            }

            int seedCount = _inventory != null ? _inventory.GetSeedCount(selected) : 0;
            return
                $"{selected.plantName}\n" +
                $"{selected.description}\n\n" +
                $"Группа: {TranslateGroup(selected.group)}\n" +
                $"Семян в рюкзаке: {seedCount}\n" +
                BuildCellForecastBlock(selected) +
                $"Синергия: {FormatGroups(selected.synergisticGroups)}\n" +
                $"Конфликт: {FormatGroups(selected.conflictingGroups)}\n" +
                $"Мутации и цена: {BuildEventMutationValueGuide(selected)}";
        }

        private string BuildCellForecastBlock(PlantData selected)
        {
            GridCell cell = _interaction != null ? _interaction.CurrentCell ?? _interaction.NearestCell : null;
            if (selected == null)
            {
                return string.Empty;
            }

            if (cell == null)
            {
                return "Прогноз для клетки: подойдите к грядке, чтобы увидеть локальный расчет\n";
            }

            if (cell.IsDestroyed)
            {
                return $"Прогноз для клетки [{cell.X},{cell.Y}]: сначала восстановите грядку\n";
            }

            if (cell.RequiresWatering)
            {
                return $"Прогноз для клетки [{cell.X},{cell.Y}]: сначала полейте грядку\n";
            }

            if (!cell.IsEmpty)
            {
                return $"Прогноз для клетки [{cell.X},{cell.Y}]: занята {cell.Plant.Data.plantName}\n";
            }

            if (_gridManager == null)
            {
                return $"Прогноз для клетки [{cell.X},{cell.Y}]: нет данных\n";
            }

            var (growthBonus, yieldBonus, valueBonus) = _gridManager.CalculatePredictedNeighborBonus(cell, selected);
            string verdict = BuildPlacementVerdict(growthBonus, yieldBonus, valueBonus);
            return
                $"Клетка [{cell.X},{cell.Y}] в грядке {cell.PlotId + 1}\n" +
                $"Прогноз: рост {FormatSignedPercent(growthBonus)}, урожай {FormatSignedPercent(yieldBonus)}, цена {FormatSignedPercent(valueBonus)}\n" +
                $"Оценка места: {verdict}\n";
        }

        private string BuildEventMutationValueGuide(PlantData plant)
        {
            if (plant == null || _eventSystem == null || _eventSystem.AllEvents == null)
            {
                return "нет данных";
            }

            StringBuilder builder = new();
            bool hasAny = false;
            IReadOnlyList<EventData> events = _eventSystem.AllEvents;
            for (int i = 0; i < events.Count; i++)
            {
                EventData eventData = events[i];
                MutationData mutation = eventData != null ? eventData.eventMutation : null;
                if (mutation == null || !EventAffectsPlantGroup(eventData, plant.group))
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

        private static string BuildSaleAdvice(float saleValue, float baselineValue)
        {
            if (baselineValue <= 0.01f)
            {
                return "оценка цены недоступна";
            }

            float ratio = saleValue / baselineValue;
            if (ratio >= 1.2f)
            {
                return "выгодно продавать";
            }

            if (ratio >= 0.95f)
            {
                return "цена близка к базовой";
            }

            return "цена просела из-за слабых мутаций";
        }

        private static string BuildPlacementVerdict(float growthBonus, float yieldBonus, float valueBonus)
        {
            float total = growthBonus + yieldBonus + valueBonus;
            if (total >= 0.35f)
            {
                return "очень выгодная посадка";
            }

            if (total >= 0.1f)
            {
                return "место дает заметную синергию";
            }

            if (total <= -0.45f)
            {
                return "жесткий конфликт, сажать невыгодно";
            }

            if (total <= -0.15f)
            {
                return "есть конфликт с соседями";
            }

            return "нейтральное место";
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

        private static string FormatSignedPercent(float value)
        {
            float percent = value * 100f;
            string sign = percent >= 0f ? "+" : string.Empty;
            return $"{sign}{percent:F0}%";
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

        private static string FormatGroups(PlantGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
            {
                return "нет";
            }

            List<string> translated = new(groups.Length);
            for (int i = 0; i < groups.Length; i++)
            {
                translated.Add(TranslateGroup(groups[i]));
            }

            return string.Join(", ", translated);
        }
    }
}
