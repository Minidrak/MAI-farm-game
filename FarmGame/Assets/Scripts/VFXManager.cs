using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FarmSimulator.Data;
using EventType = FarmSimulator.Data.EventType;

namespace FarmSimulator.Visual
{
    /// <summary>
    /// Manages VFX Graph effect spawning.
    /// Attach to a persistent GameObject in the scene.
    /// Set references to rain/drought/mutation VFX prefabs in the Inspector.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        [Header("Global VFX Prefabs (VFX Graph)")]
        [SerializeField] private GameObject _rainVFXPrefab;
        [SerializeField] private GameObject _droughtVFXPrefab;
        [SerializeField] private GameObject _pestVFXPrefab;
        [SerializeField] private GameObject _anomalyVFXPrefab;

        [Header("Mutation VFX")]
        [SerializeField] private GameObject _commonMutationVFX;
        [SerializeField] private GameObject _rareMutationVFX;
        [SerializeField] private GameObject _uniqueMutationVFX;

        [Header("VFX lifetime (seconds)")]
        [SerializeField] private float _mutationVFXLifetime = 3f;

        private readonly Dictionary<string, GameObject> _activeGlobalVFX = new();

        // ── Event VFX ─────────────────────────────────────────────────────────
        public void PlayEventVFX(Core.ActiveEvent active)
        {
            var data = active.Data;

            // If the EventData has its own prefab, use that
            GameObject prefab = data.vfxPrefab;

            // Fallback to built-in mapping
            if (prefab == null)
            {
                prefab = data.type switch
                {
                    EventType.Weather    => SelectWeatherVFX(data),
                    EventType.Biological => _pestVFXPrefab,
                    EventType.Anomalous  => _anomalyVFXPrefab,
                    _ => null
                };
            }

            if (prefab == null) return;

            var key = data.eventName;
            if (_activeGlobalVFX.ContainsKey(key)) return;

            var instance = Instantiate(prefab, Vector3.up * 2f, Quaternion.identity);
            _activeGlobalVFX[key] = instance;
        }

        public void StopEventVFX(Core.ActiveEvent active)
        {
            var key = active.Data.eventName;
            if (!_activeGlobalVFX.TryGetValue(key, out var instance)) return;

            Destroy(instance);
            _activeGlobalVFX.Remove(key);
        }

        // ── Mutation VFX ──────────────────────────────────────────────────────
        public void PlayMutationVFX(Core.GridCell cell, MutationData mutation)
        {
            GameObject prefab = mutation.vfxPrefab;

            if (prefab == null)
            {
                prefab = mutation.rarity switch
                {
                    MutationRarity.Common => _commonMutationVFX,
                    MutationRarity.Rare   => _rareMutationVFX,
                    MutationRarity.Unique => _uniqueMutationVFX,
                    _ => _commonMutationVFX
                };
            }

            if (prefab == null) return;

            var instance = Instantiate(prefab, cell.transform.position + Vector3.up, Quaternion.identity);
            StartCoroutine(DestroyAfter(instance, _mutationVFXLifetime));
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private GameObject SelectWeatherVFX(EventData data)
        {
            // Simple heuristic: positive moisture → rain, negative → drought
            return data.effect.soilMoistureDelta >= 0 ? _rainVFXPrefab : _droughtVFXPrefab;
        }

        private IEnumerator DestroyAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null) Destroy(go);
        }
    }
}
