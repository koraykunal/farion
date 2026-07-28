using System.IO;

namespace Farion.Core.Persistence
{
    public static class SaveGameFileService
    {
        const string TemporaryFileExtension = ".tmp";
        const string BackupFileExtension = ".bak";

        public static SaveGameOperationResult WriteText(string slotName, string payload)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            if (string.IsNullOrWhiteSpace(resolvedSlotName))
            {
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.InvalidSlotName, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.EmptyPayload, string.Empty);
            }

            string path = SaveGameSlotCatalog.GetSlotPath(resolvedSlotName);
            string tempPath = path + TemporaryFileExtension;
            string backupPath = path + BackupFileExtension;

            try
            {
                Directory.CreateDirectory(SaveGameSlotCatalog.GetSaveDirectory());
                File.WriteAllText(tempPath, payload);

                if (File.Exists(path))
                {
                    TryDelete(backupPath);
                    File.Replace(tempPath, path, backupPath);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return SaveGameOperationResult.Success(path);
            }
            catch (IOException exception)
            {
                TryDelete(tempPath);
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.IoError, path, exception);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                TryDelete(tempPath);
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.IoError, path, exception);
            }
        }

        public static SaveGameOperationResult ReadText(string slotName, out string payload)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            if (string.IsNullOrWhiteSpace(resolvedSlotName))
            {
                payload = string.Empty;
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.InvalidSlotName, string.Empty);
            }

            string path = SaveGameSlotCatalog.GetSlotPath(resolvedSlotName);
            return ReadPath(path, out payload);
        }

        public static SaveGameOperationResult ReadBackupText(string slotName, out string payload)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            if (string.IsNullOrWhiteSpace(resolvedSlotName))
            {
                payload = string.Empty;
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.InvalidSlotName, string.Empty);
            }

            string path = SaveGameSlotCatalog.GetSlotPath(resolvedSlotName) + BackupFileExtension;
            return ReadPath(path, out payload);
        }

        public static SaveGameOperationResult DeleteSlot(string slotName)
        {
            string resolvedSlotName = SaveGameSlotCatalog.ResolveSlotName(slotName);
            if (string.IsNullOrWhiteSpace(resolvedSlotName))
            {
                return SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.InvalidSlotName,
                    string.Empty);
            }

            string path = SaveGameSlotCatalog.GetSlotPath(resolvedSlotName);
            string backupPath = path + BackupFileExtension;
            string tempPath = path + TemporaryFileExtension;
            if (!File.Exists(path) &&
                !File.Exists(backupPath) &&
                !File.Exists(tempPath))
            {
                return SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.NoSaveFound,
                    path);
            }

            try
            {
                DeleteIfPresent(path);
                DeleteIfPresent(backupPath);
                DeleteIfPresent(tempPath);
                return SaveGameOperationResult.Success(path);
            }
            catch (IOException exception)
            {
                return SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.IoError,
                    path,
                    exception);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                return SaveGameOperationResult.Failure(
                    SaveGameOperationStatus.IoError,
                    path,
                    exception);
            }
        }

        static SaveGameOperationResult ReadPath(string path, out string payload)
        {
            payload = string.Empty;
            if (!File.Exists(path))
            {
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.NoSaveFound, path);
            }

            try
            {
                payload = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(payload)
                    ? SaveGameOperationResult.Failure(SaveGameOperationStatus.EmptyPayload, path)
                    : SaveGameOperationResult.Success(path);
            }
            catch (IOException exception)
            {
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.IoError, path, exception);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                return SaveGameOperationResult.Failure(SaveGameOperationStatus.IoError, path, exception);
            }
        }

        static void TryDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }
        }

        static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
