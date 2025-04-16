// using System.Text.RegularExpressions;
//
// namespace TimeStretch.Utils
// {
//     public static class AudioClipUtils
//     {
//         private static readonly Regex WeaponClipRegex = new Regex(
//             @"^(?<weapon>[a-z0-9_]+)_((indoor|outdoor)_)?close(_silenced)?_loop(_tail)?$",
//             RegexOptions.IgnoreCase | RegexOptions.Compiled
//         );
//         
//         public static string ExtractWeaponNameFromClip(string clipName)
//         {
//             if (string.IsNullOrWhiteSpace(clipName))
//                 return null;
//
//             var match = WeaponClipRegex.Match(clipName);
//             if (!match.Success)
//                 return null;
//
//             return match.Groups["weapon"].Value;
//         }
//     }
// }