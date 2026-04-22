using System.Collections.Generic;
using System.Text;
using UnityEngine;
using FarmSimulator.Core;
using FarmSimulator.Data;
using FarmSimulator.Player;

namespace FarmSimulator.UI
{
    public class VerticalSliceUI : MonoBehaviour
    {
        [SerializeField] private EconomyManager _economy;
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private InteractionController _interaction;

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _titleStyle;
        private Vector2 _historyScroll;

        private void OnGUI()
        {
            EnsureStyles();

            DrawHud();
            DrawSelection();
            DrawHistory();
        }

        private void DrawHud()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 210f), _panelStyle);
            GUILayout.Label("Ферма мутаций", _titleStyle);
            GUILayout.Label($"Золото: {_economy.Gold:F0}", _labelStyle);
            GUILayout.Label($"Заработано: {_economy.TotalEarned:F0}", _labelStyle);
            GUILayout.Space(6f);
            GUILayout.Label($"Действие: {_interaction.GetInteractionPrompt()}", _labelStyle);
            GUILayout.Label("Q / R: сменить семя", _labelStyle);
            GUILayout.Label("WASD: движение", _labelStyle);
            GUILayout.Label("E: взаимодействие", _labelStyle);

            if (_eventSystem.ActiveEvents.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Активные события", _titleStyle);
                foreach (ActiveEvent active in _eventSystem.ActiveEvents)
                {
                    GUILayout.Label($"{active.Data.eventName} ({active.TimeRemaining:F0}с)", _labelStyle);
                }
            }
            else
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Следующее событие через {_eventSystem.NextEventTimer:F0}с", _labelStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawSelection()
        {
            GUILayout.BeginArea(new Rect(16f, 236f, 420f, 260f), _panelStyle);
            GUILayout.Label("Выбранная ячейка", _titleStyle);

            GridCell cell = _interaction.CurrentCell;
            if (cell == null)
            {
                GUILayout.Label("Нет ячейки в радиусе взаимодействия", _labelStyle);
            }
            else if (cell.IsEmpty)
            {
                GUILayout.Label($"Грядка {cell.PlotId + 1} / [{cell.X},{cell.Y}]", _labelStyle);
                GUILayout.Label($"Почва: плодородие {cell.Soil.fertility:P0}, влажность {cell.Soil.moisture:P0}", _labelStyle);
                GUILayout.Space(6f);
                GUILayout.Label("Семена", _titleStyle);

                IReadOnlyList<PlantData> plants = _interaction.AvailablePlants;
                for (int i = 0; i < plants.Count; i++)
                {
                    PlantData plant = plants[i];
                    string marker = plant == _interaction.SelectedPlant ? "> " : "  ";
                    GUILayout.Label($"{marker}{plant.plantName}  {plant.seedCost:F0}g", _labelStyle);
                }
            }
            else
            {
                PlantInstance plant = cell.Plant;
                GUILayout.Label(plant.Data.plantName, _titleStyle);
                GUILayout.Label($"Состояние: {TranslateState(plant.State)}", _labelStyle);
                GUILayout.Label($"Рост: {plant.Growth * 100f:F0}%", _labelStyle);
                GUILayout.Label($"Урожай: {plant.CalculateYield():F1}", _labelStyle);
                GUILayout.Label($"Стоимость: {plant.CalculateValue():F0}g", _labelStyle);

                StringBuilder builder = new();
                builder.Append("Мутации: ");
                if (plant.Mutations.Count == 0)
                {
                    builder.Append("нет");
                }
                else
                {
                    for (int i = 0; i < plant.Mutations.Count; i++)
                    {
                        if (i > 0) builder.Append(", ");
                        builder.Append(plant.Mutations[i].mutationName);
                    }
                }

                GUILayout.Label(builder.ToString(), _labelStyle);
                GUILayout.Label($"Почва: плодородие {cell.Soil.fertility:P0}, влажность {cell.Soil.moisture:P0}, заражение {cell.Soil.infection:P0}", _labelStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawHistory()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 340f, 16f, 320f, 280f), _panelStyle);
            GUILayout.Label("Последние продажи", _titleStyle);
            _historyScroll = GUILayout.BeginScrollView(_historyScroll, false, true);

            IReadOnlyList<SaleRecord> history = _economy.History;
            if (history.Count == 0)
            {
                GUILayout.Label("Пока ничего не продано", _labelStyle);
            }
            else
            {
                for (int i = history.Count - 1; i >= 0 && i >= history.Count - 8; i--)
                {
                    SaleRecord record = history[i];
                    GUILayout.Label($"{record.PlantName}: {record.Value:F0}g", _labelStyle);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string TranslateState(PlantState state)
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
                richText = true
            };

            _titleStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
