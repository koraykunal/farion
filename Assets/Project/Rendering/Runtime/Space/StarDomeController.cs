using UnityEngine;

namespace Farion.Rendering.Space
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class StarDomeController : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] StarDomeProfile profile;

        [Header("Render Settings")]
        [SerializeField] bool applyOnEnable = true;
        [SerializeField] bool updateInEditMode = true;
        [SerializeField] bool updateEveryFrame;
        [SerializeField] bool updateEnvironmentLighting;

        StarDomeProfile subscribedProfile;

        void OnEnable()
        {
            SyncProfileSubscription();

            if (applyOnEnable)
            {
                ApplyStarDome();
            }
        }

        void OnValidate()
        {
            SyncProfileSubscription();

            if (!Application.isPlaying && updateInEditMode)
            {
                ApplyStarDome();
            }
        }

        void LateUpdate()
        {
            if (!updateEveryFrame)
            {
                return;
            }

            if (Application.isPlaying || updateInEditMode)
            {
                ApplyStarDome();
            }
        }

        void OnDisable()
        {
            UnsubscribeFromProfile();
        }

        [ContextMenu("Apply Star Dome Now")]
        public void ApplyStarDome()
        {
            if (profile == null || profile.SkyboxMaterial == null)
            {
                return;
            }

            profile.ApplyTo(profile.SkyboxMaterial);
            RenderSettings.skybox = profile.SkyboxMaterial;

            if (updateEnvironmentLighting)
            {
                DynamicGI.UpdateEnvironment();
            }
        }

        void SyncProfileSubscription()
        {
            if (subscribedProfile == profile)
            {
                return;
            }

            UnsubscribeFromProfile();
            subscribedProfile = profile;
            if (subscribedProfile != null)
            {
                subscribedProfile.Changed += HandleProfileChanged;
            }
        }

        void UnsubscribeFromProfile()
        {
            if (subscribedProfile == null)
            {
                return;
            }

            subscribedProfile.Changed -= HandleProfileChanged;
            subscribedProfile = null;
        }

        void HandleProfileChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (Application.isPlaying || updateInEditMode)
            {
                ApplyStarDome();
            }
        }
    }
}
