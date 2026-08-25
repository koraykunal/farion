using Farion.Gameplay.Flight;
using Farion.Gameplay.Presentation.Flight;
using Farion.Gameplay.Presentation.Lighting;
using Farion.Rendering.Lighting;
using UnityEditor;
using UnityEngine;

namespace Farion.Editor.Authoring
{
    static class FarionFloodlightAuthoring
    {
        const string ShuttlePath = "Assets/Project/Prefabs/Gameplay/Spacecraft/PF_PlayerStarterShuttle.prefab";
        const string NetworkShuttlePath = "Assets/Project/Prefabs/Multiplayer/PF_PlayerStarterShuttleNetwork.prefab";
        const string ProfileFolder = "Assets/Project/Design/Gameplay/Lighting";
        const string FloodProfilePath = ProfileFolder + "/SO_ShuttleFloodlight.asset";
        const string AlarmProfilePath = ProfileFolder + "/SO_HullAlarmBeacon.asset";
        const string CookiePath = "Assets/Project/Art/Textures/Lighting/TX_Light_FloodlightCookie.png";

        [MenuItem("Farion/Authoring/Apply Ship Floodlights")]
        public static void Apply()
        {
            Texture cookie = AssetDatabase.LoadAssetAtPath<Texture>(CookiePath);
            if (cookie == null)
            {
                Debug.LogError($"Missing {CookiePath}. Run Farion/Authoring/Generate Floodlight Cookie first.");
                return;
            }

            LightFixtureProfile flood = WriteFloodProfile(cookie);
            LightFixtureProfile alarm = WriteAlarmProfile();

            if (!ApplyToShuttle(flood, alarm))
            {
                return;
            }

            ApplyToNetworkVariant();
            AssetDatabase.SaveAssets();
            Debug.Log("Ship floodlights applied.");
        }

        static bool ApplyToShuttle(LightFixtureProfile flood, LightFixtureProfile alarm)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ShuttlePath);
            try
            {
                Transform visualRoot = root.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    Debug.LogError("VisualRoot not found on the shuttle prefab.");
                    return false;
                }

                Transform mount = root.transform.Find("Anchors");
                if (mount == null)
                {
                    Debug.LogError("Anchors not found on the shuttle prefab.");
                    return false;
                }

                PurgeFixtures(root.transform);

                if (!TryComputeHullBounds(visualRoot, out Bounds hull))
                {
                    Debug.LogError("No mesh renderers under VisualRoot to place fixtures against.");
                    return false;
                }

                Bounds nose = TryFindColliderBounds(root.transform, "COL_Hull_Nose", out Bounds noseBounds)
                    ? noseBounds
                    : hull;
                Debug.Log($"Hull bounds {hull.center} +/- {hull.extents}, nose bounds {nose.center} +/- {nose.extents}.");

                float sideOffset = nose.extents.x * 0.55f;
                float noseZ = nose.max.z - 0.2f;
                float lampY = nose.center.y - nose.extents.y * 0.3f;
                float topY = hull.max.y - 0.15f;

                LightFixture floodLeft = BuildFixture(
                    mount,
                    "LT_Floodlight_L",
                    new Vector3(nose.center.x - sideOffset, lampY, noseZ),
                    new Vector3(3f, 0f, 0f),
                    flood,
                    alarm,
                    0.11f);
                LightFixture floodRight = BuildFixture(
                    mount,
                    "LT_Floodlight_R",
                    new Vector3(nose.center.x + sideOffset, lampY, noseZ),
                    new Vector3(3f, 0f, 0f),
                    flood,
                    alarm,
                    0.63f);
                LightFixture beaconForward = BuildFixture(
                    mount,
                    "LT_Beacon_Top_F",
                    new Vector3(hull.center.x, topY, Mathf.Lerp(hull.min.z, hull.max.z, 0.7f)),
                    new Vector3(55f, 0f, 0f),
                    alarm,
                    null,
                    0.29f);
                LightFixture beaconAft = BuildFixture(
                    mount,
                    "LT_Beacon_Top_A",
                    new Vector3(hull.center.x, topY, Mathf.Lerp(hull.min.z, hull.max.z, 0.28f)),
                    new Vector3(55f, 180f, 0f),
                    alarm,
                    null,
                    0.81f);

                SpacecraftFloodlights floodlights = Ensure<SpacecraftFloodlights>(root);
                SerializedObject serializedFloodlights = new(floodlights);
                SetArray(serializedFloodlights, "fixtures", floodLeft, floodRight);
                serializedFloodlights.FindProperty("allowManualToggle").boolValue = true;
                serializedFloodlights.FindProperty("startOn").boolValue = false;
                serializedFloodlights.FindProperty("inputSource").objectReferenceValue =
                    root.GetComponent<KeyboardSpacecraftInput>();
                serializedFloodlights.ApplyModifiedPropertiesWithoutUndo();

                SpacecraftHullAlarm hullAlarm = Ensure<SpacecraftHullAlarm>(root);
                SerializedObject serializedAlarm = new(hullAlarm);
                SetArray(
                    serializedAlarm,
                    "fixtures",
                    beaconForward,
                    beaconAft,
                    floodLeft,
                    floodRight);
                serializedAlarm.FindProperty("hull").objectReferenceValue =
                    root.GetComponent<SpacecraftHull>();
                serializedAlarm.FindProperty("fullFlickerDamage").floatValue = 60f;
                serializedAlarm.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ShuttlePath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ApplyToNetworkVariant()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NetworkShuttlePath);
            try
            {
                SpacecraftFloodlights floodlights = root.GetComponent<SpacecraftFloodlights>();
                if (floodlights == null)
                {
                    Debug.LogWarning("Network shuttle variant has no SpacecraftFloodlights to override.");
                    return;
                }

                SerializedObject serialized = new(floodlights);
                serialized.FindProperty("allowManualToggle").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, NetworkShuttlePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool TryComputeHullBounds(Transform visualRoot, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate is not MeshRenderer && candidate is not SkinnedMeshRenderer)
                {
                    continue;
                }

                if (candidate.GetComponentInParent<ParticleSystem>() != null ||
                    candidate.GetComponentInParent<SpacecraftThrusterNozzleVfx>() != null ||
                    candidate.GetComponent<SpacecraftThrusterMeshLayer>() != null ||
                    IsUnderNamed(candidate.transform, "VFX"))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = candidate.bounds;
                    found = true;
                    continue;
                }

                bounds.Encapsulate(candidate.bounds);
            }

            return found;
        }

        static void PurgeFixtures(Transform root)
        {
            LightFixture[] existing = root.GetComponentsInChildren<LightFixture>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                Object.DestroyImmediate(existing[i].gameObject);
            }
        }

        static bool IsUnderNamed(Transform target, string ancestorName)
        {
            for (Transform current = target; current != null; current = current.parent)
            {
                if (current.name == ancestorName)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryFindColliderBounds(Transform root, string colliderName, out Bounds bounds)
        {
            bounds = default;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].name != colliderName)
                {
                    continue;
                }

                bounds = colliders[i].bounds;
                return true;
            }

            return false;
        }

        static LightFixture BuildFixture(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localEuler,
            LightFixtureProfile profile,
            LightFixtureProfile alarmProfile,
            float seed)
        {
            Transform existing = parent.Find(name);
            GameObject host = existing != null ? existing.gameObject : new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = localPosition;
            host.transform.localEulerAngles = localEuler;
            host.transform.localScale = Vector3.one;

            Light core = EnsureChildLight(host.transform, "Core");
            Light spill = EnsureChildLight(host.transform, "Spill");
            ApplyLightPreview(core, profile, isSpill: false);
            ApplyLightPreview(spill, profile, isSpill: true);

            LightFixture fixture = Ensure<LightFixture>(host);
            SerializedObject serialized = new(fixture);
            serialized.FindProperty("profile").objectReferenceValue = profile;
            serialized.FindProperty("alarmProfile").objectReferenceValue = alarmProfile;
            serialized.FindProperty("coreLight").objectReferenceValue = core;
            serialized.FindProperty("spillLight").objectReferenceValue = spill;
            serialized.FindProperty("seed").floatValue = seed;
            serialized.FindProperty("startOn").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (profile.UnderwaterScatteringStrength > 0f)
            {
                UnderwaterSpotLight underwaterLight = Ensure<UnderwaterSpotLight>(host);
                SerializedObject serializedUnderwaterLight = new(underwaterLight);
                serializedUnderwaterLight.FindProperty("source").objectReferenceValue = core;
                serializedUnderwaterLight.FindProperty("scatteringStrength").floatValue =
                    profile.UnderwaterScatteringStrength;
                serializedUnderwaterLight.ApplyModifiedPropertiesWithoutUndo();
            }

            return fixture;
        }

        static Light EnsureChildLight(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject host = existing != null ? existing.gameObject : new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            return Ensure<Light>(host);
        }

        static void ApplyLightPreview(Light target, LightFixtureProfile profile, bool isSpill)
        {
            target.type = LightType.Spot;
            target.color = profile.Color;
            target.useColorTemperature = profile.UseColorTemperature;
            target.colorTemperature = profile.ColorTemperature;
            target.enabled = false;

            if (isSpill)
            {
                target.intensity = profile.Intensity * profile.SpillIntensityRatio;
                target.range = profile.Range * profile.SpillRangeRatio;
                target.spotAngle = profile.SpillAngle;
                target.innerSpotAngle = profile.SpillAngle * 0.4f;
                target.shadows = LightShadows.None;
                target.cookie = null;
                return;
            }

            target.intensity = profile.Intensity;
            target.range = profile.Range;
            target.spotAngle = profile.SpotAngle;
            target.innerSpotAngle = profile.InnerSpotAngle;
            target.shadows = profile.Shadows;
            target.shadowStrength = profile.ShadowStrength;
            target.cookie = profile.Cookie;
        }

        static LightFixtureProfile WriteFloodProfile(Texture cookie)
        {
            LightFixtureProfile profile = LoadOrCreateProfile(FloodProfilePath);
            SerializedObject serialized = new(profile);
            serialized.FindProperty("useColorTemperature").boolValue = true;
            serialized.FindProperty("colorTemperature").floatValue = 4300f;
            serialized.FindProperty("color").colorValue = Color.white;
            serialized.FindProperty("intensity").floatValue = 1800f;
            serialized.FindProperty("range").floatValue = 250f;
            serialized.FindProperty("spotAngle").floatValue = 36f;
            serialized.FindProperty("innerSpotAngle").floatValue = 20f;
            serialized.FindProperty("spillIntensityRatio").floatValue = 0.14f;
            serialized.FindProperty("spillAngle").floatValue = 105f;
            serialized.FindProperty("spillRangeRatio").floatValue = 0.2f;
            serialized.FindProperty("shadows").enumValueIndex = (int)LightShadows.Soft;
            serialized.FindProperty("shadowStrength").floatValue = 0.85f;
            serialized.FindProperty("underwaterScatteringStrength").floatValue = 0.85f;
            serialized.FindProperty("cookie").objectReferenceValue = cookie;
            serialized.FindProperty("lensEmissionColor").colorValue = new Color(1f, 0.96f, 0.9f, 1f);
            serialized.FindProperty("lensEmissionIntensity").floatValue = 6f;
            serialized.FindProperty("turnOnSeconds").floatValue = 0.25f;
            serialized.FindProperty("turnOffSeconds").floatValue = 0.09f;
            serialized.FindProperty("pulseHz").floatValue = 0f;
            serialized.FindProperty("pulseFloor").floatValue = 0.08f;
            serialized.FindProperty("pulseSharpness").floatValue = 2.5f;
            serialized.FindProperty("faultDecay").floatValue = 6f;
            serialized.FindProperty("faultFrequency").floatValue = 22f;
            serialized.FindProperty("faultDepth").floatValue = 0.85f;
            serialized.FindProperty("alarmFaultLevel").floatValue = 0.45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static LightFixtureProfile WriteAlarmProfile()
        {
            LightFixtureProfile profile = LoadOrCreateProfile(AlarmProfilePath);
            SerializedObject serialized = new(profile);
            serialized.FindProperty("useColorTemperature").boolValue = false;
            serialized.FindProperty("colorTemperature").floatValue = 1800f;
            serialized.FindProperty("color").colorValue = new Color(1f, 0.06f, 0.03f, 1f);
            serialized.FindProperty("intensity").floatValue = 150f;
            serialized.FindProperty("range").floatValue = 60f;
            serialized.FindProperty("spotAngle").floatValue = 120f;
            serialized.FindProperty("innerSpotAngle").floatValue = 40f;
            serialized.FindProperty("spillIntensityRatio").floatValue = 0.25f;
            serialized.FindProperty("spillAngle").floatValue = 160f;
            serialized.FindProperty("spillRangeRatio").floatValue = 0.35f;
            serialized.FindProperty("shadows").enumValueIndex = (int)LightShadows.None;
            serialized.FindProperty("shadowStrength").floatValue = 0.6f;
            serialized.FindProperty("cookie").objectReferenceValue = null;
            serialized.FindProperty("lensEmissionColor").colorValue = new Color(4f, 0.15f, 0.06f, 1f);
            serialized.FindProperty("lensEmissionIntensity").floatValue = 2f;
            serialized.FindProperty("turnOnSeconds").floatValue = 0.05f;
            serialized.FindProperty("turnOffSeconds").floatValue = 0.12f;
            serialized.FindProperty("pulseHz").floatValue = 1.6f;
            serialized.FindProperty("pulseFloor").floatValue = 0.06f;
            serialized.FindProperty("pulseSharpness").floatValue = 2.5f;
            serialized.FindProperty("faultDecay").floatValue = 5f;
            serialized.FindProperty("faultFrequency").floatValue = 18f;
            serialized.FindProperty("faultDepth").floatValue = 0.6f;
            serialized.FindProperty("alarmFaultLevel").floatValue = 0.3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static LightFixtureProfile LoadOrCreateProfile(string path)
        {
            LightFixtureProfile existing = AssetDatabase.LoadAssetAtPath<LightFixtureProfile>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
            {
                AssetDatabase.CreateFolder("Assets/Project/Design/Gameplay", "Lighting");
            }

            LightFixtureProfile created = ScriptableObject.CreateInstance<LightFixtureProfile>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static void SetArray(SerializedObject serialized, string field, params Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        static T Ensure<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : host.AddComponent<T>();
        }
    }
}
