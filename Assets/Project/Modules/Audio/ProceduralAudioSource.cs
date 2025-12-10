using UnityEngine;
using System.Collections;

namespace PCG.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralAudioSource : MonoBehaviour
    {
        private double _frequency;
        private double _increment;
        private double _phase;
        private double _samplingFrequency = 48000;
        private float _gain; // Volume
        private float _targetVolume;

        private void Start()
        {
            _samplingFrequency = AudioSettings.outputSampleRate;
        }

        // This method receives a certain frequency and duration and plays a note with a decay effect
        public void PlayNote(float frequency, float duration)
        {
            _frequency = frequency;
            _targetVolume = 0.5f; // Max volume of that note

            StartCoroutine(Decay(duration));
        }

        // This method transitions from X volume to 0, to decay a note
        private IEnumerator Decay(float duration)
        {
            float time = 0;

            while (time < duration)
            {
                time += Time.deltaTime;
                _targetVolume = Mathf.Lerp(0.5f, 0f, time / duration); // Transition from volume to silence, "ting" effect
                yield return null;
            }

            _targetVolume = 0;
        }

        // This method mathematically creates sound waves
        private void OnAudioFilterRead(float[] data, int channels)
        {
            _increment = _frequency * 2 * System.Math.PI / _samplingFrequency;

            for (int i = 0; i < data.Length; i += channels)
            {
                _phase += _increment;

                float sample = (float)System.Math.Sin(_phase); // Generates a Sinus wave (pure sound) -> It can be changed to either Cube or Triangle to 8-bit sound

                if (_gain < _targetVolume)
                {
                    _gain += 0.05f;
                }
                else if (_gain > _targetVolume)
                {
                    _gain -= 0.05f;
                }

                data[i] = sample * _gain;

                if (channels == 2) // If stereo, copy to second channel
                {
                    data[i + 1] = data[i];
                }

                if (_phase > System.Math.PI * 2)
                {
                    _phase = 0;
                }
            }
        }
    }
}