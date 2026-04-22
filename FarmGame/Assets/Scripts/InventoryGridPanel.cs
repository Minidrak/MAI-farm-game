using System.Collections.Generic;
using FarmSimulator.Core;
using FarmSimulator.Data;
using FarmSimulator.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FarmSimulator.UI
{
    public class InventoryGridPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryManager _inventory;
        [SerializeField] private InteractionController _interaction;
        [SerializeField] private Text _detailsText;
        [SerializeField] private RectTransform _gridRoot;

        [Header("Colors")]
        [SerializeField] private Color _emptyColor = new(0.18f, 0.2f, 0.18f, 0.95f);
        [SerializeField] private Color _seedColor = new(0.28f, 0.52f, 0.24f, 0.98f);
        [SerializeField] private Color _cropColor = new(0.67f, 0.43f, 0.2f, 0.98f);
        [SerializeField] private Color _selectedPlantOutline = new(1f, 0.88f, 0.36f, 1f);
        [SerializeField] private Color _normalOutline = new(0f, 0f, 0f, 0.25f);

        private readonly List<Image> _slotImages = new();
        private readonly List<Text> _slotTexts = new();

        private void Awake()
        {
            EnsureSlotViews();
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
            if (_inventory == null || _gridRoot == null)
            {
                return;
            }

            EnsureSlotViews();
            PlantData selectedPlant = _interaction != null ? _interaction.SelectedPlant : null;
            IReadOnlyList<InventorySlotData> slots = _inventory.Slots;

            for (int i = 0; i < _slotImages.Count; i++)
            {
                Image image = _slotImages[i];
                Text label = _slotTexts[i];
                InventorySlotData slot = i < slots.Count ? slots[i] : null;

                if (slot == null || slot.IsEmpty)
                {
                    image.color = _emptyColor;
                    label.text = $"{i + 1:00}\n-";
                }
                else
                {
                    bool isSeed = slot.kind == InventoryItemKind.Seed;
                    image.color = isSeed ? _seedColor : _cropColor;
                    label.text = BuildSlotLabel(slot);
                }

                Outline outline = image.GetComponent<Outline>();
                if (outline != null)
                {
                    bool highlight = slot != null && !slot.IsEmpty && selectedPlant != null && slot.plant == selectedPlant;
                    outline.effectColor = highlight ? _selectedPlantOutline : _normalOutline;
                }
            }

            if (_detailsText != null)
            {
                _detailsText.text = BuildDetailsText(selectedPlant);
            }
        }

        private void EnsureSlotViews()
        {
            if (_inventory == null || _gridRoot == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (_slotImages.Count < _inventory.SlotCount)
            {
                int index = _slotImages.Count;
                GameObject slot = new($"Slot_{index + 1:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                slot.transform.SetParent(_gridRoot, false);

                Image image = slot.GetComponent<Image>();
                image.color = _emptyColor;

                Outline outline = slot.GetComponent<Outline>();
                outline.effectDistance = new Vector2(1f, -1f);
                outline.effectColor = _normalOutline;

                GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(slot.transform, false);
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(4f, 4f);
                labelRect.offsetMax = new Vector2(-4f, -4f);

                Text label = labelObject.GetComponent<Text>();
                label.font = font;
                label.fontSize = 11;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.color = new Color(0.98f, 0.98f, 0.95f, 1f);

                _slotImages.Add(image);
                _slotTexts.Add(label);
            }
        }

        private string BuildDetailsText(PlantData selectedPlant)
        {
            if (_inventory == null)
            {
                return "Инвентарь недоступен";
            }

            if (selectedPlant == null)
            {
                return _inventory.HasWateringCan
                    ? "Канистра куплена. Выбери культуру Q / R, чтобы увидеть связанные слоты."
                    : "Выбери культуру Q / R, чтобы увидеть связанные слоты и цены.";
            }

            InventorySlotData bestCrop = FindBestCropSlot(selectedPlant);

            return
                $"{selectedPlant.plantName}\n" +
                $"Группа: {TranslateGroup(selectedPlant.group)}\n" +
                $"Семян: {_inventory.GetSeedCount(selectedPlant)}\n" +
                $"Урожая: {_inventory.GetCropCount(selectedPlant)}\n" +
                $"Цена семени: {selectedPlant.seedCost:F0}g\n" +
                $"База продажи: {selectedPlant.baseValue:F0}g\n" +
                $"Синергия: {FormatGroups(selectedPlant.synergisticGroups)}\n" +
                $"Конфликт: {FormatGroups(selectedPlant.conflictingGroups)}\n" +
                $"Цена урожая зависит от мутаций и их редкости" +
                (bestCrop != null
                    ? $"\nЛучшая партия: {bestCrop.saleValue:F0}g • x{bestCrop.valueMultiplier:F2}\n{BuildSaleAdvice(bestCrop.saleValue, selectedPlant.baseValue * selectedPlant.baseYield)}"
                    : "\nПока нет собранных партий");
        }

        private static string BuildSlotLabel(InventorySlotData slot)
        {
            string prefix = slot.kind == InventoryItemKind.Seed ? "S" : "C";
            string shortName = slot.plant != null && slot.plant.plantName.Length > 8
                ? slot.plant.plantName.Substring(0, 8)
                : slot.DisplayName;
            return slot.kind == InventoryItemKind.Crop
                ? $"{prefix}\n{shortName}\nx{slot.quantity}\nx{slot.valueMultiplier:F2}"
                : $"{prefix}\n{shortName}\nx{slot.quantity}";
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
                return "Оценка цены недоступна";
            }

            float ratio = saleValue / baselineValue;
            if (ratio >= 1.2f)
            {
                return "Выгодная партия";
            }

            if (ratio >= 0.95f)
            {
                return "Цена близка к базовой";
            }

            return "Есть уценка из-за слабых мутаций";
        }

        private static string FormatGroups(PlantGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
            {
                return "нет";
            }

            List<string> names = new(groups.Length);
            for (int i = 0; i < groups.Length; i++)
            {
                names.Add(groups[i] switch
                {
                    PlantGroup.Vegetables => "Овощи",
                    PlantGroup.Fruits => "Фрукты",
                    PlantGroup.Grains => "Злаки",
                    PlantGroup.Exotic => "Экзотика",
                    _ => groups[i].ToString()
                });
            }

            return string.Join(", ", names);
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
    }
}
