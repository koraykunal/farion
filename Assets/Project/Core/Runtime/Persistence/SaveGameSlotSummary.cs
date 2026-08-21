using System;

namespace Farion.Core.Persistence
{
    public enum SaveGameSlotState
    {
        Empty = 0,
        Available = 10,
        RecoverableBackup = 20,
        Unsupported = 30,
        Invalid = 40
    }

    public readonly struct SaveGameSlotSummary
    {
        public SaveGameSlotSummary(
            string slotName,
            string path,
            SaveGameSlotState state,
            int schemaVersion,
            DateTime? savedAtUtc,
            DateTime? fileUpdatedAtUtc,
            bool multiplayerSession = false)
        {
            SlotName = slotName ?? string.Empty;
            Path = path ?? string.Empty;
            State = state;
            SchemaVersion = schemaVersion;
            SavedAtUtc = savedAtUtc;
            FileUpdatedAtUtc = fileUpdatedAtUtc;
            MultiplayerSession = multiplayerSession;
        }

        public string SlotName { get; }
        public string Path { get; }
        public SaveGameSlotState State { get; }
        public int SchemaVersion { get; }
        public DateTime? SavedAtUtc { get; }
        public DateTime? FileUpdatedAtUtc { get; }
        public bool MultiplayerSession { get; }
        public DateTime? BestTimestampUtc => SavedAtUtc ?? FileUpdatedAtUtc;
        public bool HasData => State != SaveGameSlotState.Empty;
        public bool IsLoadable =>
            State is SaveGameSlotState.Available or
                SaveGameSlotState.RecoverableBackup;
        public bool UsesBackup => State == SaveGameSlotState.RecoverableBackup;
    }
}
