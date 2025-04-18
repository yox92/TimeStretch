using System;

namespace ChangeMetaDataBundle
{
    public static class UserPrompt
    {
        public static bool AskUserForModification()
        {
            Console.WriteLine("❓ Do you want to modify or restore the bundles?");
            Console.WriteLine("Type [Y] to MODIFY, [N] to RESTORE, then press Enter.");

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim().ToUpperInvariant();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (input is "Y" or "YES")
                    return true;

                if (input is "N" or "NO")
                    return false;

                Console.WriteLine("⚠️ Invalid input. Please type Y or N.");
            }
        }
    }
}