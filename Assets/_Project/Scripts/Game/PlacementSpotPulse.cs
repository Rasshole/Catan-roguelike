using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>v143: subtle breathing scale for placement spot circles / road dashes.</summary>
    public sealed class PlacementSpotPulse : MonoBehaviour
    {
        public float Amplitude = 0.09f;
        public float Speed = 3.2f;
        private float _phase;

        private void Awake() => _phase = Random.value * 0.6f;

        private void Update()
        {
            float k = 1f + Amplitude * Mathf.Sin((Time.unscaledTime + _phase) * Speed);
            transform.localScale = new Vector3(k, 1f, k);
        }
    }
}
