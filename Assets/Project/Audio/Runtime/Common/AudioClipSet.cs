using UnityEngine;

namespace Farion.Audio
{
    [CreateAssetMenu(menuName = "Farion/Audio/Audio Clip Set", fileName = "SO_AudioClipSet")]
    public sealed class AudioClipSet : ScriptableObject
    {
        [SerializeField] AudioClip[] clips;
        [SerializeField] Vector2 volumeRange = new(0.9f, 1f);
        [SerializeField] Vector2 pitchRange = new(0.98f, 1.02f);

        public bool TryGetRandom(out AudioClip clip, out float volume, out float pitch)
        {
            if (clips == null || clips.Length == 0)
            {
                clip = null;
                volume = 0f;
                pitch = 1f;
                return false;
            }

            clip = clips[Random.Range(0, clips.Length)];
            volume = Random.Range(volumeRange.x, volumeRange.y);
            pitch = Random.Range(pitchRange.x, pitchRange.y);
            return clip != null;
        }

        void OnValidate()
        {
            volumeRange = ClampRange(volumeRange, 0f, 1f);
            pitchRange = ClampRange(pitchRange, 0.01f, 3f);
        }

        static Vector2 ClampRange(Vector2 range, float min, float max)
        {
            float x = Mathf.Clamp(range.x, min, max);
            float y = Mathf.Clamp(range.y, min, max);
            return x <= y ? new Vector2(x, y) : new Vector2(y, x);
        }
    }
}
