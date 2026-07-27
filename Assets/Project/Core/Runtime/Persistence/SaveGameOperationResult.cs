using System;

namespace Farion.Core.Persistence
{
    public readonly struct SaveGameOperationResult
    {
        public SaveGameOperationResult(SaveGameOperationStatus status, string path, string error)
        {
            Status = status;
            Path = path ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public SaveGameOperationStatus Status { get; }
        public string Path { get; }
        public string Error { get; }
        public bool Succeeded => Status == SaveGameOperationStatus.Succeeded;

        public static SaveGameOperationResult Success(string path)
        {
            return new SaveGameOperationResult(SaveGameOperationStatus.Succeeded, path, string.Empty);
        }

        public static SaveGameOperationResult Failure(SaveGameOperationStatus status, string path, Exception exception = null)
        {
            return new SaveGameOperationResult(status, path, exception != null ? exception.Message : string.Empty);
        }
    }
}
