using System.Collections;
using FarmSimulator.Core;
using FarmSimulator.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace FarmSimulator.Visual
{
    public class EventVisualController : MonoBehaviour
    {
        [System.Serializable]
        private struct SceneVisualState
        {
            public Material skybox;
            public Color directionalLightColor;
            public float directionalLightIntensity;
            public AmbientMode ambientMode;
            public Color ambientColor;
            public bool fogEnabled;
            public Color fogColor;
            public float fogDensity;
        }

        [Header("References")]
        [SerializeField] private FarmEventSystem _eventSystem;
        [SerializeField] private Light _mainDirectionalLight;

        private SceneVisualState _defaultState;
        private SceneVisualState _appliedState;
        private EventData _currentVisualEvent;
        private Coroutine _transitionRoutine;
        private bool _hasCapturedDefaultState;

        private void Awake()
        {
            if (_eventSystem == null)
            {
                _eventSystem = FindFirstObjectByType<FarmEventSystem>();
            }

            if (_mainDirectionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        _mainDirectionalLight = lights[i];
                        break;
                    }
                }
            }

            CaptureDefaultState();
        }

        private void OnEnable()
        {
            if (_eventSystem != null)
            {
                _eventSystem.OnEventStarted += HandleEventStateChanged;
                _eventSystem.OnEventEnded += HandleEventStateChanged;
            }
        }

        private void Start()
        {
            RefreshVisualState(force: true);
        }

        private void Update()
        {
            RefreshVisualState(force: false);
        }

        private void OnDisable()
        {
            if (_eventSystem != null)
            {
                _eventSystem.OnEventStarted -= HandleEventStateChanged;
                _eventSystem.OnEventEnded -= HandleEventStateChanged;
            }

            if (_hasCapturedDefaultState)
            {
                ApplyStateImmediate(_defaultState);
                _appliedState = _defaultState;
                _currentVisualEvent = null;
            }
        }

        public void ApplyEventVisual(EventData eventData)
        {
            if (eventData == null)
            {
                RestoreDefaultVisual();
                return;
            }

            SceneVisualState targetState = BuildState(eventData);
            StartTransition(targetState, Mathf.Max(0.05f, eventData.blendInSeconds));
            _currentVisualEvent = eventData;
        }

        public void RestoreDefaultVisual()
        {
            CaptureDefaultState();
            StartTransition(_defaultState, Mathf.Max(0.05f, _currentVisualEvent != null ? _currentVisualEvent.blendOutSeconds : 1.8f));
            _currentVisualEvent = null;
        }

        public EventData ResolveActiveVisualEvent()
        {
            if (_eventSystem == null || _eventSystem.ActiveEvents == null || _eventSystem.ActiveEvents.Count == 0)
            {
                return null;
            }

            EventData selected = null;
            int selectedPriority = int.MinValue;
            for (int i = 0; i < _eventSystem.ActiveEvents.Count; i++)
            {
                ActiveEvent active = _eventSystem.ActiveEvents[i];
                EventData candidate = active != null ? active.Data : null;
                if (candidate == null)
                {
                    continue;
                }

                if (selected == null || candidate.visualPriority >= selectedPriority)
                {
                    selected = candidate;
                    selectedPriority = candidate.visualPriority;
                }
            }

            return selected;
        }

        private void HandleEventStateChanged(ActiveEvent _)
        {
            RefreshVisualState(force: true);
        }

        private void RefreshVisualState(bool force)
        {
            CaptureDefaultState();
            EventData resolvedEvent = ResolveActiveVisualEvent();
            if (!force && resolvedEvent == _currentVisualEvent)
            {
                return;
            }

            if (resolvedEvent == null)
            {
                RestoreDefaultVisual();
                return;
            }

            ApplyEventVisual(resolvedEvent);
        }

        private void CaptureDefaultState()
        {
            if (_hasCapturedDefaultState || _mainDirectionalLight == null)
            {
                return;
            }

            _defaultState = new SceneVisualState
            {
                skybox = RenderSettings.skybox,
                directionalLightColor = _mainDirectionalLight.color,
                directionalLightIntensity = _mainDirectionalLight.intensity,
                ambientMode = RenderSettings.ambientMode,
                ambientColor = RenderSettings.ambientLight,
                fogEnabled = RenderSettings.fog,
                fogColor = RenderSettings.fogColor,
                fogDensity = RenderSettings.fogDensity
            };

            _appliedState = _defaultState;
            _hasCapturedDefaultState = true;
        }

        private SceneVisualState BuildState(EventData eventData)
        {
            SceneVisualState target = _defaultState;
            if (eventData == null)
            {
                return target;
            }

            if (eventData.skyboxMaterial != null)
            {
                target.skybox = eventData.skyboxMaterial;
            }

            target.directionalLightColor = eventData.directionalLightColor;
            target.directionalLightIntensity = eventData.directionalLightIntensity;
            target.ambientMode = AmbientMode.Flat;
            target.ambientColor = eventData.ambientColor;
            target.fogEnabled = eventData.fogDensity > 0.0001f;
            target.fogColor = eventData.fogColor;
            target.fogDensity = eventData.fogDensity;
            return target;
        }

        private void StartTransition(SceneVisualState targetState, float duration)
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
            }

            if (duration <= 0.05f)
            {
                ApplyStateImmediate(targetState);
                _appliedState = targetState;
                return;
            }

            _transitionRoutine = StartCoroutine(TransitionRoutine(_appliedState, targetState, duration));
        }

        private IEnumerator TransitionRoutine(SceneVisualState from, SceneVisualState to, float duration)
        {
            RenderSettings.skybox = to.skybox != null ? to.skybox : from.skybox;
            RenderSettings.ambientMode = to.ambientMode;
            RenderSettings.fog = to.fogEnabled;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyInterpolatedState(from, to, t);
                yield return null;
            }

            ApplyStateImmediate(to);
            _appliedState = to;
            _transitionRoutine = null;
        }

        private void ApplyInterpolatedState(SceneVisualState from, SceneVisualState to, float t)
        {
            if (_mainDirectionalLight != null)
            {
                _mainDirectionalLight.color = Color.Lerp(from.directionalLightColor, to.directionalLightColor, t);
                _mainDirectionalLight.intensity = Mathf.Lerp(from.directionalLightIntensity, to.directionalLightIntensity, t);
            }

            RenderSettings.ambientLight = Color.Lerp(from.ambientColor, to.ambientColor, t);
            RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t);

            DynamicGI.UpdateEnvironment();
        }

        private void ApplyStateImmediate(SceneVisualState state)
        {
            if (_mainDirectionalLight != null)
            {
                _mainDirectionalLight.color = state.directionalLightColor;
                _mainDirectionalLight.intensity = state.directionalLightIntensity;
            }

            RenderSettings.skybox = state.skybox;
            RenderSettings.ambientMode = state.ambientMode;
            RenderSettings.ambientLight = state.ambientColor;
            RenderSettings.fog = state.fogEnabled;
            RenderSettings.fogColor = state.fogColor;
            RenderSettings.fogDensity = state.fogDensity;
            DynamicGI.UpdateEnvironment();
        }
    }
}
