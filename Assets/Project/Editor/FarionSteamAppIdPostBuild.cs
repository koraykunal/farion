using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Farion.Editor
{
    public sealed class FarionSteamAppIdPostBuild : IPostprocessBuildWithReport
    {
        const string FileName = "steam_appid.txt";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string source = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                FileName);
            string outputDirectory =
                Path.GetDirectoryName(report.summary.outputPath);
            if (!File.Exists(source) || string.IsNullOrEmpty(outputDirectory))
            {
                return;
            }

            File.Copy(source, Path.Combine(outputDirectory, FileName), true);
        }
    }
}
