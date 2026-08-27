using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Farion.Editor.Validation
{
    public sealed partial class FarionProjectValidator
    {
        const string ShaderSearchRoot = "Assets/Project/Art/Shaders";

        static void ValidateShaders(FarionValidationReport report)
        {
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { ShaderSearchRoot });
            if (guids.Length == 0)
            {
                report.AddError($"No shaders found under {ShaderSearchRoot}.");
                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    report.AddError($"{path} could not be loaded as a shader.");
                    continue;
                }

                if (!ShaderUtil.ShaderHasError(shader))
                {
                    continue;
                }

                ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
                for (int m = 0; m < messages.Length; m++)
                {
                    if (messages[m].severity != ShaderCompilerMessageSeverity.Error)
                    {
                        continue;
                    }

                    report.AddError(
                        $"{path}: {messages[m].message} ({messages[m].messageDetails.Trim()})");
                }

                if (messages.Length == 0)
                {
                    report.AddError($"{path}: shader failed to compile.");
                }
            }
        }
    }
}
