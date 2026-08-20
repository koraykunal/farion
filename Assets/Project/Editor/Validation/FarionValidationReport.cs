using System.Collections.Generic;
using UnityEngine;

namespace Farion.Editor.Validation
{
    public sealed class FarionValidationReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool HasErrors => Errors.Count > 0;

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void Log()
        {
            foreach (string error in Errors)
            {
                Debug.LogError($"[Farion Validation] {error}");
            }

            foreach (string warning in Warnings)
            {
                Debug.LogWarning($"[Farion Validation] {warning}");
            }

            if (!HasErrors)
            {
                Debug.Log($"[Farion Validation] Passed with {Warnings.Count} warning(s).");
            }
        }
    }
}
