using System.Windows.Forms;

namespace ChangeMetaDataBundle
{
    public static class FolderSelector
    {
        public static string? AskUserForTarkovRoot()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select the Escape From Tarkov root folder (containing EscapeFromTarkov.exe)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }
    }
}