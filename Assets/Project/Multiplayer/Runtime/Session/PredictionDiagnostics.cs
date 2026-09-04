using UnityEngine;

namespace Farion.Multiplayer.Session
{
    public static class PredictionDiagnostics
    {
        static float worstPositionError;
        static float worstRotationError;
        static float lastPositionError;

        public static float WorstPositionError => worstPositionError;
        public static float WorstRotationError => worstRotationError;
        public static float LastPositionError => lastPositionError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            worstPositionError = 0f;
            worstRotationError = 0f;
            lastPositionError = 0f;
        }

        public static void ReportReconcile(
            Vector3 predictedPosition,
            Quaternion predictedRotation,
            Vector3 authoritativePosition,
            Quaternion authoritativeRotation)
        {
            float positionError =
                Vector3.Distance(predictedPosition, authoritativePosition);
            float rotationError =
                Quaternion.Angle(predictedRotation, authoritativeRotation);
            lastPositionError = positionError;
            if (positionError > worstPositionError)
            {
                worstPositionError = positionError;
            }

            if (rotationError > worstRotationError)
            {
                worstRotationError = rotationError;
            }
        }
    }
}
