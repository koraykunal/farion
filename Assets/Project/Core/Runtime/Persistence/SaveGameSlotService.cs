using System;
using System.Collections.Generic;
using System.IO;

namespace Farion.Core.Persistence
{
    public static class SaveGameSlotService
    {
        public static IReadOnlyList<SaveGameSlotSummary> GetPlayerSlotSummaries()
        {
            IReadOnlyList<string> slotNames = SaveGameSlotCatalog.PlayerSlotNames;
            List<SaveGameSlotSummary> summaries = new(slotNames.Count);
            for (int i = 0; i < slotNames.Count; i++)
            {
                summaries.Add(GetSummary(slotNames[i]));
            }

            return summaries;
        }

        public static SaveGameSlotSummary GetSummary(string slotName)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            SaveGameOperationResult primaryResult =
                SaveGameFileService.ReadText(resolvedSlotName, out string primaryPayload);
            SaveGameSlotSummary primarySummary = CreateSummary(
                resolvedSlotName,
                primaryResult,
                primaryPayload,
                fromBackup: false);
            if (primarySummary.IsLoadable)
            {
                return primarySummary;
            }

            SaveGameOperationResult backupResult =
                SaveGameFileService.ReadBackupText(resolvedSlotName, out string backupPayload);
            SaveGameSlotSummary backupSummary = CreateSummary(
                resolvedSlotName,
                backupResult,
                backupPayload,
                fromBackup: true);
            if (backupSummary.IsLoadable)
            {
                return backupSummary;
            }

            if (primarySummary.HasData)
            {
                return primarySummary;
            }

            return backupSummary.HasData
                ? backupSummary
                : new SaveGameSlotSummary(
                    resolvedSlotName,
                    SaveGameSlotCatalog.GetSlotPath(resolvedSlotName),
                    SaveGameSlotState.Empty,
                    0,
                    null,
                    null);
        }

        public static bool TryGetMostRecentLoadable(
            out SaveGameSlotSummary mostRecent)
        {
            IReadOnlyList<SaveGameSlotSummary> summaries =
                GetPlayerSlotSummaries();
            bool found = false;
            mostRecent = default;
            for (int i = 0; i < summaries.Count; i++)
            {
                SaveGameSlotSummary candidate = summaries[i];
                if (!candidate.IsLoadable)
                {
                    continue;
                }

                if (!found ||
                    ResolveTimestamp(candidate) > ResolveTimestamp(mostRecent))
                {
                    mostRecent = candidate;
                    found = true;
                }
            }

            return found;
        }

        static SaveGameSlotSummary CreateSummary(
            string slotName,
            SaveGameOperationResult readResult,
            string payload,
            bool fromBackup)
        {
            if (!readResult.Succeeded)
            {
                return readResult.Status == SaveGameOperationStatus.NoSaveFound
                    ? new SaveGameSlotSummary(
                        slotName,
                        readResult.Path,
                        SaveGameSlotState.Empty,
                        0,
                        null,
                        null)
                    : new SaveGameSlotSummary(
                        slotName,
                        readResult.Path,
                        SaveGameSlotState.Invalid,
                        0,
                        null,
                        TryGetLastWriteTimeUtc(readResult.Path));
            }

            if (!SaveGameSchema.TryReadHeader(
                    payload,
                    out int schemaVersion,
                    out DateTime? savedAtUtc))
            {
                return new SaveGameSlotSummary(
                    slotName,
                    readResult.Path,
                    SaveGameSlotState.Invalid,
                    0,
                    null,
                    TryGetLastWriteTimeUtc(readResult.Path));
            }

            SaveGameSlotState state = SaveGameSchema.IsSupportedVersion(schemaVersion)
                ? fromBackup
                    ? SaveGameSlotState.RecoverableBackup
                    : SaveGameSlotState.Available
                : SaveGameSlotState.Unsupported;
            return new SaveGameSlotSummary(
                slotName,
                readResult.Path,
                state,
                schemaVersion,
                savedAtUtc,
                TryGetLastWriteTimeUtc(readResult.Path));
        }

        static DateTime ResolveTimestamp(SaveGameSlotSummary summary)
        {
            return summary.BestTimestampUtc ?? DateTime.MinValue;
        }

        static DateTime? TryGetLastWriteTimeUtc(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return File.GetLastWriteTimeUtc(path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
