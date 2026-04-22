using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FarmSimulator
{
    public enum AudioCue
    {
        UiSelect,
        Error,
        Buy,
        Sell,
        Plant,
        Harvest,
        Water,
        Repair,
        Mutation,
        EventGoodStart,
        EventBadStart,
        EventEnd,
        Footstep
    }

    /// <summary>
    /// Lightweight procedural audio layer so gameplay gets immediate feedback
    /// even before real sound assets are imported.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        public static AudioManager Instance { get; private set; }

        [Header("Volumes")]
        [SerializeField] private float _masterVolume = 0.8f;
        [SerializeField] private float _uiVolume = 0.7f;
        [SerializeField] private float _worldVolume = 0.85f;
        [SerializeField] private float _footstepVolume = 0.45f;

        private readonly Dictionary<AudioCue, AudioClip> _clips = new();
        private readonly Dictionary<AudioCue, AudioClip[]> _clipVariants = new();
        private AudioSource _uiSource;
        private AudioSource _worldSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureSources();
            BuildClipCache();
#if UNITY_EDITOR
            LoadEditorImportedOverrides();
#endif
        }

        public void Play(AudioCue cue, float volumeScale = 1f)
        {
            AudioClip clip = GetClip(cue);
            if (clip == null)
            {
                return;
            }

            AudioSource source = cue == AudioCue.Footstep ? _worldSource : _uiSource;
            float baseVolume = cue == AudioCue.Footstep ? _footstepVolume : _uiVolume;
            source.PlayOneShot(clip, baseVolume * _masterVolume * volumeScale);
        }

        public void PlayAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
            AudioClip clip = GetClip(cue);
            if (clip == null)
            {
                return;
            }

            GameObject audioObject = new($"AudioCue_{cue}");
            audioObject.transform.position = position;
            var source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 12f;
            source.volume = _worldVolume * _masterVolume * volumeScale;
            source.Play();
            Destroy(audioObject, clip.length + 0.05f);
        }

        public void PlayEventStart(bool positiveEvent)
        {
            Play(positiveEvent ? AudioCue.EventGoodStart : AudioCue.EventBadStart);
        }

        private AudioClip GetClip(AudioCue cue)
        {
            if (_clipVariants.TryGetValue(cue, out AudioClip[] variants) && variants != null && variants.Length > 0)
            {
                int index = variants.Length == 1 ? 0 : Random.Range(0, variants.Length);
                if (variants[index] != null)
                {
                    return variants[index];
                }
            }

            _clips.TryGetValue(cue, out AudioClip fallback);
            return fallback;
        }

        private void EnsureSources()
        {
            _uiSource = GetOrCreateSource("UISource", spatialBlend: 0f);
            _worldSource = GetOrCreateSource("WorldSource", spatialBlend: 0f);
        }

        private AudioSource GetOrCreateSource(string sourceName, float spatialBlend)
        {
            Transform child = transform.Find(sourceName);
            GameObject target = child != null ? child.gameObject : new GameObject(sourceName);
            if (child == null)
            {
                target.transform.SetParent(transform, false);
            }

            AudioSource source = target.GetComponent<AudioSource>();
            if (source == null)
            {
                source = target.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            return source;
        }

        private void BuildClipCache()
        {
            _clips[AudioCue.UiSelect] = CreateToneSweep("UiSelect", 0.08f, 900f, 1250f, Waveform.Sine, 0.4f);
            _clips[AudioCue.Error] = CreateDoubleTone("Error", 0.16f, 420f, 260f, Waveform.Square, 0.28f);
            _clips[AudioCue.Buy] = CreateDoubleTone("Buy", 0.18f, 740f, 980f, Waveform.Sine, 0.38f);
            _clips[AudioCue.Sell] = CreateCoinTone("Sell", 0.15f, 840f, 1200f, 0.4f);
            _clips[AudioCue.Plant] = CreateNoiseHit("Plant", 0.18f, new Color(0.35f, 0.24f, 0.12f), 0.42f);
            _clips[AudioCue.Harvest] = CreatePop("Harvest", 0.14f, 520f, 0.42f);
            _clips[AudioCue.Water] = CreateNoiseHit("Water", 0.24f, new Color(0.25f, 0.45f, 0.8f), 0.36f);
            _clips[AudioCue.Repair] = CreateHammer("Repair", 0.20f, 220f, 0.45f);
            _clips[AudioCue.Mutation] = CreateShimmer("Mutation", 0.38f, 420f, 780f, 0.35f);
            _clips[AudioCue.EventGoodStart] = CreatePad("EventGoodStart", 0.65f, 340f, 510f, 0.32f);
            _clips[AudioCue.EventBadStart] = CreatePad("EventBadStart", 0.7f, 180f, 120f, 0.36f);
            _clips[AudioCue.EventEnd] = CreateToneSweep("EventEnd", 0.28f, 620f, 460f, Waveform.Sine, 0.26f);
            _clips[AudioCue.Footstep] = CreateFootstep("Footstep", 0.07f, 0.32f);
        }

#if UNITY_EDITOR
        private void LoadEditorImportedOverrides()
        {
            const string root = "Assets/Casual Game Sounds U6/CasualGameSounds";
            if (!AssetDatabase.IsValidFolder(root))
            {
                return;
            }

            AssignVariants(AudioCue.UiSelect, root, "DM-CGS-20.wav", "DM-CGS-21.wav", "DM-CGS-40.wav");
            AssignVariants(AudioCue.Error, root, "DM-CGS-34.wav", "DM-CGS-44.wav");
            AssignVariants(AudioCue.Buy, root, "DM-CGS-16.wav", "DM-CGS-31.wav", "DM-CGS-42.wav");
            AssignVariants(AudioCue.Sell, root, "DM-CGS-17.wav", "DM-CGS-18.wav", "DM-CGS-26.wav");
            AssignVariants(AudioCue.Plant, root, "DM-CGS-27.wav", "DM-CGS-29.wav", "DM-CGS-30.wav");
            AssignVariants(AudioCue.Harvest, root, "DM-CGS-36.wav", "DM-CGS-37.wav", "DM-CGS-38.wav");
            AssignVariants(AudioCue.Water, root, "DM-CGS-24.wav", "DM-CGS-33.wav");
            AssignVariants(AudioCue.Repair, root, "DM-CGS-23.wav", "DM-CGS-24.wav");
            AssignVariants(AudioCue.Mutation, root, "DM-CGS-43.wav", "DM-CGS-49.wav", "DM-CGS-50.wav");
            AssignVariants(AudioCue.EventGoodStart, root, "DM-CGS-11.wav", "DM-CGS-45.wav");
            AssignVariants(AudioCue.EventBadStart, root, "DM-CGS-09.wav", "DM-CGS-48.wav");
            AssignVariants(AudioCue.EventEnd, root, "DM-CGS-28.wav", "DM-CGS-50.wav");
            AssignVariants(AudioCue.Footstep, root, "DM-CGS-14.wav", "DM-CGS-15.wav", "DM-CGS-22.wav", "DM-CGS-41.wav", "DM-CGS-47.wav");
        }

        private void AssignVariants(AudioCue cue, string root, params string[] fileNames)
        {
            var loaded = new List<AudioClip>();
            for (int i = 0; i < fileNames.Length; i++)
            {
                string path = $"{root}/{fileNames[i]}";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    loaded.Add(clip);
                }
            }

            if (loaded.Count > 0)
            {
                _clipVariants[cue] = loaded.ToArray();
            }
        }
#endif

        private static AudioClip CreateToneSweep(string name, float duration, float startFrequency, float endFrequency, Waveform waveform, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float phase = 2f * Mathf.PI * frequency * t;
                samples[i] = SampleWaveform(phase, waveform) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateDoubleTone(string name, float duration, float firstFrequency, float secondFrequency, Waveform waveform, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];
            int split = sampleCount / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float frequency = i < split ? firstFrequency : secondFrequency;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float phase = 2f * Mathf.PI * frequency * t;
                samples[i] = SampleWaveform(phase, waveform) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateCoinTone(string name, float duration, float firstFrequency, float secondFrequency, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Exp(-6f * progress);
                float phaseA = 2f * Mathf.PI * Mathf.Lerp(firstFrequency, secondFrequency, progress) * t;
                float phaseB = 2f * Mathf.PI * Mathf.Lerp(firstFrequency * 1.9f, secondFrequency * 1.4f, progress) * t;
                samples[i] = (Mathf.Sin(phaseA) + Mathf.Sin(phaseB) * 0.45f) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateNoiseHit(string name, float duration, Color tonalHint, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];
            float seed = tonalHint.r * 13.7f + tonalHint.g * 19.1f + tonalHint.b * 23.4f;

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Exp(-7f * progress);
                float noise = Mathf.PerlinNoise(seed, i * 0.071f) * 2f - 1f;
                float lowTone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 60f, progress) * (i / (float)SampleRate));
                samples[i] = (noise * 0.75f + lowTone * 0.25f) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreatePop(string name, float duration, float baseFrequency, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Exp(-9f * progress);
                float phase = 2f * Mathf.PI * Mathf.Lerp(baseFrequency, baseFrequency * 0.6f, progress) * t;
                samples[i] = Mathf.Sin(phase) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateHammer(string name, float duration, float baseFrequency, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Exp(-10f * progress);
                float metallic = Mathf.Sin(2f * Mathf.PI * baseFrequency * t) + Mathf.Sin(2f * Mathf.PI * baseFrequency * 2.2f * t) * 0.35f;
                float thump = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.4f;
                samples[i] = (metallic + thump) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateShimmer(string name, float duration, float baseFrequency, float endFrequency, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float freq = Mathf.Lerp(baseFrequency, endFrequency, progress);
                float signal = Mathf.Sin(2f * Mathf.PI * freq * t);
                signal += Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.45f;
                signal += Mathf.Sin(2f * Mathf.PI * freq * 2.2f * t) * 0.2f;
                samples[i] = signal * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreatePad(string name, float duration, float rootFrequency, float harmonyFrequency, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Sin(progress * Mathf.PI) * Mathf.Lerp(0.3f, 1f, progress);
                float signal = Mathf.Sin(2f * Mathf.PI * rootFrequency * t) * 0.65f;
                signal += Mathf.Sin(2f * Mathf.PI * harmonyFrequency * t) * 0.35f;
                signal += Mathf.Sin(2f * Mathf.PI * (rootFrequency * 0.5f) * t) * 0.18f;
                samples[i] = signal * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static AudioClip CreateFootstep(string name, float duration, float amplitude)
        {
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)(sampleCount - 1);
                float envelope = Mathf.Exp(-18f * progress);
                float noise = Mathf.PerlinNoise(0.17f, i * 0.19f) * 2f - 1f;
                float thump = Mathf.Sin(2f * Mathf.PI * 85f * (i / (float)SampleRate));
                samples[i] = (noise * 0.45f + thump * 0.55f) * envelope * amplitude;
            }

            return CreateClip(name, samples);
        }

        private static float SampleWaveform(float phase, Waveform waveform)
        {
            return waveform switch
            {
                Waveform.Square => Mathf.Sign(Mathf.Sin(phase)),
                _ => Mathf.Sin(phase)
            };
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private enum Waveform
        {
            Sine,
            Square
        }
    }
}
