using System.IO;
using Farion.Core.Persistence;
using NUnit.Framework;

namespace Farion.Tests.EditMode
{
    public sealed class SaveGameFileServiceTests
    {
        const string Slot = "editmode-file-service";

        [TearDown]
        public void TearDown()
        {
            SaveGameFileService.DeleteSlot(Slot);
        }

        [Test]
        public void SecondWriteKeepsThePreviousPayloadAsBackup()
        {
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"v\":1}").Succeeded, Is.True);
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"v\":2}").Succeeded, Is.True);

            Assert.That(SaveGameFileService.ReadText(Slot, out string current).Succeeded, Is.True);
            Assert.That(SaveGameFileService.ReadBackupText(Slot, out string backup).Succeeded, Is.True);
            Assert.That(current, Is.EqualTo("{\"v\":2}"));
            Assert.That(backup, Is.EqualTo("{\"v\":1}"));
            Assert.That(File.Exists(SaveGameSlotCatalog.GetSlotPath(Slot) + ".tmp"), Is.False);
        }

        [Test]
        public void EmptyPayloadAndInvalidSlotAreRejectedWithoutTouchingTheFile()
        {
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"v\":1}").Succeeded, Is.True);
            SaveGameOperationResult empty = SaveGameFileService.WriteText(Slot, "   ");
            Assert.That(empty.Status, Is.EqualTo(SaveGameOperationStatus.EmptyPayload));
            Assert.That(SaveGameFileService.ReadText(Slot, out string current).Succeeded, Is.True);
            Assert.That(current, Is.EqualTo("{\"v\":1}"));

            Assert.That(
                SaveGameFileService.ReadText("missing-" + Slot, out _).Status,
                Is.EqualTo(SaveGameOperationStatus.NoSaveFound));
        }

        [Test]
        public void DeleteRemovesPrimaryBackupAndTemporaryFiles()
        {
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"v\":1}").Succeeded, Is.True);
            Assert.That(SaveGameFileService.WriteText(Slot, "{\"v\":2}").Succeeded, Is.True);
            string path = SaveGameSlotCatalog.GetSlotPath(Slot);
            File.WriteAllText(path + ".tmp", "partial");

            Assert.That(SaveGameFileService.DeleteSlot(Slot).Succeeded, Is.True);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".bak"), Is.False);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
            Assert.That(
                SaveGameFileService.DeleteSlot(Slot).Status,
                Is.EqualTo(SaveGameOperationStatus.NoSaveFound));
        }
    }
}
