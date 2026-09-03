using UnityEngine;

namespace Farion.Gameplay.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class VisorEyes : MonoBehaviour
    {
        [Header("Color")]
        [Tooltip("Tint used until a session identity arrives; multiplayer replaces it with a hue hashed from the player id.")]
        [ColorUsage(false, true)]
        [SerializeField] Color color = new(0.35f, 2.2f, 2.6f, 1f);
        [Tooltip("Emission multiplier for hashed identity hues so they land at the same brightness as the fallback tint.")]
        [Min(0f)]
        [SerializeField] float identityIntensity = 2.5f;

        [Header("Blink")]
        [Tooltip("Random seconds between blinks, min and max.")]
        [SerializeField] Vector2 blinkInterval = new(2.5f, 6f);
        [Min(0.02f)]
        [SerializeField] float blinkDuration = 0.14f;

        [Header("Expression")]
        [Tooltip("Eye openness while airborne; above 1 widens past the resting circle.")]
        [Min(0.05f)]
        [SerializeField] float airborneOpen = 1.3f;
        [Tooltip("Seconds to ease between expressions.")]
        [Min(0.01f)]
        [SerializeField] float expressionSmoothing = 0.12f;

        static readonly int EyeColorId = Shader.PropertyToID("_EyeColor");
        static readonly int EyeOpenId = Shader.PropertyToID("_EyeOpen");

        Renderer target;
        ExplorerLocomotionSignals signals;
        MaterialPropertyBlock block;
        Color resolvedColor;
        bool hasIdentity;
        float nextBlinkTime;
        float blinkStartTime = float.NegativeInfinity;
        float expression = 1f;

        void Awake()
        {
            target = GetComponent<Renderer>();
            signals = GetComponentInParent<ExplorerLocomotionSignals>();
            block = new MaterialPropertyBlock();
            if (!hasIdentity)
            {
                resolvedColor = color;
            }

            ScheduleBlink(Time.time);
        }

        public void SetIdentity(ulong sessionPlayerId)
        {
            hasIdentity = sessionPlayerId != 0UL;
            if (!hasIdentity)
            {
                resolvedColor = color;
                return;
            }

            float hue = Mathf.Repeat((sessionPlayerId % 4096UL) * 0.6180339887f, 1f);
            resolvedColor = Color.HSVToRGB(hue, 0.6f, 1f) * identityIntensity;
        }

        void Update()
        {
            if (!target.enabled)
            {
                return;
            }

            float now = Time.time;
            if (now >= nextBlinkTime)
            {
                blinkStartTime = now;
                ScheduleBlink(now);
            }

            float blink = Mathf.Clamp01((now - blinkStartTime) / blinkDuration);
            float lid = 1f - Mathf.Sin(blink * Mathf.PI);

            bool airborne = signals != null && !signals.LastState.Grounded;
            float goal = airborne ? airborneOpen : 1f;
            expression = Mathf.MoveTowards(
                expression,
                goal,
                Time.deltaTime / expressionSmoothing);

            target.GetPropertyBlock(block);
            block.SetColor(EyeColorId, resolvedColor);
            block.SetFloat(EyeOpenId, expression * lid);
            target.SetPropertyBlock(block);
        }

        void ScheduleBlink(float now)
        {
            nextBlinkTime = now + Random.Range(blinkInterval.x, blinkInterval.y);
        }
    }
}
