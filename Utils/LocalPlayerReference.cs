using EFT;

namespace TimeStretch.Utils;

public abstract class LocalPlayerReference
{
    public static bool IsInitialized => Player != null;

    public static IPlayer Player { get; private set; }

    public static string ProfileId { get; private set; }

    public static string AccountId { get; private set; }

    public static bool TryInitialize(Player player)
    {
        if (Player != null) return false;
        if (player == null || !player.IsYourPlayer) return false;

        Player = player;
        ProfileId = player.ProfileId;
        AccountId = player.AccountId;

        BatchLogger.Log($"[LocalPlayerReference] ✅ Local player initialized : {ProfileId} / {AccountId}");
        return true;
    }    
    // FIKA friendly
    public static bool IsLocalPlayer(IPlayer p)
    {
        return p is Player player && player.IsYourPlayer;
    }

    public static void Reset()
    {
        Player = null;
        ProfileId = null;
        AccountId = null;
        BatchLogger.Warn("[LocalPlayerReference] 🧹 Local player reset.");
    }
}