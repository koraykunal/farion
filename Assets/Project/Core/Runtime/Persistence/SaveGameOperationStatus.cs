namespace Farion.Core.Persistence
{
    public enum SaveGameOperationStatus
    {
        None = 0,
        Succeeded = 1,
        InvalidSlotName = 2,
        NoSaveFound = 3,
        EmptyPayload = 4,
        InvalidPayload = 5,
        MissingRuntimeReference = 6,
        IoError = 7,
        UnsupportedVersion = 8,
        ApplyFailed = 9,
        RollbackFailed = 10,
        IncompatibleContent = 11
    }
}
