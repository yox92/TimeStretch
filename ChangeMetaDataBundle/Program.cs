using System;
using System.IO;

namespace ChangeMetaDataBundle
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (!File.Exists(PathsFile.LogFilePath))
            {
                try
                {
                    File.WriteAllText(PathsFile.LogFilePath, $"[INIT] Log created at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Unable to create log file: {ex.Message}");
                    BatchLogger.Error($"❌ Unable to create log file: {ex.Message}");
                    return;
                }
            }

            // Ask user
            bool restoreOriginal = !UserPrompt.AskUserForModification();
            AudioClipOriginalValues.Mode = restoreOriginal ? AudioClipMode.Original : AudioClipMode.Modified;

            BatchLogger.Init();
            BatchLogger.Section(
                restoreOriginal
                    ? "USER SELECTED: RESTORE ORIGINAL STATE"
                    : "USER SELECTED: APPLY MODIFICATIONS"
            );

            try
            {
                var batch = new BatchBundleModifier();
                batch.ReadApplyChange();
            }
            catch (Exception ex)
            {
                BatchLogger.LogException(ex, "Main");
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                BatchLogger.Error($"❌ Fatal error: {ex.Message}");
            }

            BatchLogger.Section("PROGRAM ENDED");
            BatchLogger.OnApplicationQuit();

            Console.WriteLine("\n✅ Done. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
