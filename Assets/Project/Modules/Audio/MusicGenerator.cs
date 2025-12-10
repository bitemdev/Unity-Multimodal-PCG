using UnityEngine;
using Unity.Collections;
using PCG.Core;

namespace PCG.Audio
{
    public class MusicGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PCGManager _pcgManager;
        [SerializeField] private Core.AudioConfiguration _audioConfig;
        [SerializeField] private PCGConfiguration _pcgConfig;
        [SerializeField] private ProceduralAudioSource _synth;

        private float _timer;
        private float _stepDuration;
        private int _currentStep;
        private int[] _sequencerPattern = new int[256]; // Procedurally created score (256 steps -> 16 compasses of 4/4 -> 32 seconds)
        private bool _isPlaying;

        private void Start()
        {
            // 60 seconds / BPM / 4 (for sixteenth notes)
            _stepDuration = 60f / _audioConfig.BPM / 4f; // Calculate 1 step duration (sixteenth note)
        }

        private void OnEnable()
        {
            if (_pcgManager != null)
            {
                _pcgManager.OnLevelGenerated += ComposeMusic;
            }
        }
        
        private void OnDisable()
        {
            if (_pcgManager != null)
            {
                _pcgManager.OnLevelGenerated -= ComposeMusic;
            }
        }
        
        // This method creates the song based on a specific map
        private void ComposeMusic(NativeList<SpawnPoint> spawnPoints)
        {
            int musicSeed = _pcgConfig.Seed + spawnPoints.Length; // It uses the map seed + number of spawn points of that specific map to make the music "fit" the map

            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)musicSeed);
            
            // Generate step pattern
            for (int i = 0; i < _sequencerPattern.Length; i++)
            {
                // Composition Algorithm
                bool isStrongBeat = (i % 4 == 0); // Every 4 steps
                float noteProbability = isStrongBeat ? 0.8f : 0.4f; // If strong beat -> 80% note probability, if not -> 40%

                if (rng.NextFloat() < noteProbability)
                {
                    // Choose note: 20% bass note (indices 0-4), 60% melody note (indices 5-9), 20% high note (indices 10-15)

                    float rangeRoll = rng.NextFloat();
                    int noteIndex = 0;

                    if (rangeRoll < 0.2f)
                    {
                        noteIndex = rng.NextInt(0, 5); // Bass
                    }
                    else if (rangeRoll < 0.8f)
                    {
                        noteIndex = rng.NextInt(5, 10); // Melody
                    }
                    else
                    {
                        noteIndex = rng.NextInt(10, _audioConfig.PentatonicScale.Length); // High
                    }

                    _sequencerPattern[i] = noteIndex;
                }
                else
                {
                    _sequencerPattern[i] = -1;
                }
            }

            _currentStep = 0;
            _isPlaying = true;
            Debug.Log("Procedurally generated music");
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _timer += Time.deltaTime;

            if (_timer >= _stepDuration)
            {
                _timer = 0;
                PlayStep();
            }
        }

        // This method plays a step
        private void PlayStep()
        {
            int noteIndex = _sequencerPattern[_currentStep]; // Read score

            if (noteIndex != -1 && noteIndex < _audioConfig.PentatonicScale.Length) // Not silence, it's a note
            {
                float frequency = _audioConfig.PentatonicScale[noteIndex];
                float duration = noteIndex < 5 ? 0.25f : 0.1f;
                _synth.PlayNote(frequency, duration);
            }

            _currentStep = (_currentStep + 1) % _sequencerPattern.Length;
        }
    }
}
