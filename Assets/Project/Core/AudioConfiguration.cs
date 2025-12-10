using UnityEngine;

namespace PCG.Core
{
    // This class defines the "rules" for the music with musical scales
    [CreateAssetMenu(menuName = "PCG/Audio Configuration")]
    public class AudioConfiguration : ScriptableObject
    {
        public int BPM => _bpm;
        public float[] PentatonicScale => _pentatonicScale;
        
        [Header("General")] 
        [SerializeField, Range(60, 180)] private int _bpm = 120;

        [Header("Extended Scale with 3 Octaves")]
        [SerializeField] private float[] _pentatonicScale = new float[]
        {
            65.41f, 77.78f, 87.31f, 98.00f, 116.54f,
            130.81f, 155.56f, 174.61f, 196.00f, 233.08f, 
            261.63f, 311.13f, 349.23f, 392.00f, 466.16f,
            523.25f
        }; // Base frequencies for a scale
    }
}