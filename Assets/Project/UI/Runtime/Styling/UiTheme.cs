using UnityEngine;

namespace Farion.UI.Styling
{
    [CreateAssetMenu(menuName = "Farion/UI/Theme", fileName = "SO_UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField] Color voidSurface = new(0.018f, 0.027f, 0.039f, 1f);
        [SerializeField] Color raisedSurface = new(0.043f, 0.067f, 0.09f, 1f);
        [SerializeField] Color buttonSurface = new(0.024f, 0.037f, 0.052f, 0.72f);
        [SerializeField] Color buttonSurfaceHighlighted = new(0.055f, 0.086f, 0.118f, 0.92f);
        [SerializeField] Color panelSurface = new(0.018f, 0.031f, 0.045f, 0.9f);

        [Header("Text")]
        [SerializeField] Color primaryText = new(0.88f, 0.91f, 0.93f, 1f);
        [SerializeField] Color secondaryText = new(0.54f, 0.6f, 0.65f, 0.9f);
        [SerializeField] Color supportingText = new(0.68f, 0.76f, 0.81f, 1f);

        [Header("Signals")]
        [SerializeField] Color focus = new(0.56f, 0.68f, 0.76f, 1f);
        [SerializeField] Color nominal = new(0.58f, 0.73f, 0.69f, 0.96f);
        [SerializeField] Color caution = new(1f, 0.72f, 0.24f, 0.98f);
        [SerializeField] Color critical = new(1f, 0.26f, 0.2f, 1f);

        [Header("Motion")]
        [Min(0f)]
        [SerializeField] float stateEnterDuration = 0.16f;
        [Min(0f)]
        [SerializeField] float stateExitDuration = 0.1f;
        [SerializeField] Vector2 panelHiddenOffset = new(-24f, 0f);

        public Color VoidSurface => voidSurface;
        public Color RaisedSurface => raisedSurface;
        public Color ButtonSurface => buttonSurface;
        public Color ButtonSurfaceHighlighted => buttonSurfaceHighlighted;
        public Color PanelSurface => panelSurface;
        public Color PrimaryText => primaryText;
        public Color SecondaryText => secondaryText;
        public Color SupportingText => supportingText;
        public Color Focus => focus;
        public Color Nominal => nominal;
        public Color Caution => caution;
        public Color Critical => critical;
        public float StateEnterDuration => Mathf.Max(0f, stateEnterDuration);
        public float StateExitDuration => Mathf.Max(0f, stateExitDuration);
        public Vector2 PanelHiddenOffset => panelHiddenOffset;

        void OnValidate()
        {
            stateEnterDuration = Mathf.Max(0f, stateEnterDuration);
            stateExitDuration = Mathf.Max(0f, stateExitDuration);
        }
    }
}
