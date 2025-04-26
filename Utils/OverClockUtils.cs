using TimeStretch.Cache;
using Comfort.Common;
using EFT;
using EFT.Communications;
using System.Collections;
using UnityEngine;

namespace TimeStretch.Utils;

public abstract class OverClockUtils
{
    private const int MinFireRate = 300; 
    private const int MaxFireRate = 1500;

    /// <summary>
    /// Calculates the tempo percentage for overclock based on weapon fire rates.  
    /// </summary>
    /// <param name="weaponId">The ID of the weapon to calculate tempo for</param>
    /// <returns>
    /// The tempo percentage difference between overclock and original fire rates.
    /// Returns 0 if the original rate is invalid or overclock rate cannot be found.
    /// </returns>
    public static float CalculateOverClockTempo(string weaponId)
    {
        BatchLogger.Log($"[DEBUG] CalculateOverClockTempo called with weaponId: {weaponId}");

        var fireRateOriginal = JsonCache.GetOriginalFireRate(weaponId);
        BatchLogger.Log($"[DEBUG] fireRateOriginal: {fireRateOriginal}");

        if (!CacheObject.TryGetFireRate(weaponId, out var fireRateOverClock))
            return 0f;
        BatchLogger.Log($"[DEBUG] fireRateOverClock: {fireRateOverClock}");

        if (fireRateOriginal is 0 or > 1500 or < 300)
        {
            BatchLogger.Warn($"Weapon is not registered as modifiable, ID: '{weaponId}'");
            BatchLogger.Log($"[DEBUG] Returning 0 due to invalid fireRateOriginal");
            return 0f;
        }

        var tempo = ((float)fireRateOverClock / fireRateOriginal - 1f) * 100f;
        BatchLogger.Log($"[DEBUG] Calculated tempo: {tempo}");

        BatchLogger.Info(
            $"🎼 Overclock tempo: = {tempo:+0.00;-0.00}% (overClock={fireRateOverClock}, originalRate={fireRateOriginal})");
        return tempo;
    }

    /// <summary>
    /// Temporarily speeds up the fire mode change animation
    /// </summary>
    public static bool ApplyFirearmAnimationMode()
    {
        var player = Singleton<GameWorld>.Instance?.MainPlayer;
        if (player?.HandsController is not Player.FirearmController firearmController)
            return false;

        var firearmsAnimator = firearmController.FirearmsAnimator;
        if (firearmsAnimator == null)
            return false;

        firearmsAnimator.SetAnimationSpeed(2.0f);

        firearmsAnimator.SetFireMode(firearmController.Item.SelectedFireMode);

        CoroutineRunner.Run(ResetAnimatorSpeed(firearmsAnimator));

        return true;
    }

    private static IEnumerator ResetAnimatorSpeed(ObjectInHandsAnimator firearmsAnimator)
    {
        yield return new WaitForSecondsRealtime(0.3f);

        firearmsAnimator?.SetAnimationSpeed(1.0f);
    }

    /// <summary>
    /// Decreases the fire rate according to configured increment
    /// </summary>
    public static void DecreaseFireRate(string weaponId, ref int overClockFireRate)
    {
        int decrementValue = Plugin.FireRateRange?.Value ?? 25;
        overClockFireRate -= decrementValue;
        if (overClockFireRate < MinFireRate)
            overClockFireRate = MaxFireRate;

        DisplayFireRateNotification(overClockFireRate);

        // Registers the new fire rate for current weapon
        if (!string.IsNullOrEmpty(weaponId))
        {
            CacheObject.RegisterFireRate(weaponId, overClockFireRate);
            BatchLogger.Info(
                $"[AnimationOverClock] Fire rate decrease registered: {overClockFireRate} RPM for {weaponId}");
        }
    }

    /// <summary>
    /// Displays a notification with the current fire rate
    /// </summary>
    private static void DisplayFireRateNotification(int overClockFireRate)
    {
        NotificationManagerClass.DisplayMessageNotification(
            $"OverClock: {overClockFireRate} RPM",
            ENotificationDurationType.Default,
            ENotificationIconType.Note);
    }

    /// <summary>
    /// Increases the fire rate according to configured increment 
    /// </summary>
    public static void IncreaseFireRate(string weaponId, ref int overClockFireRate)
    {
        var incrementValue = Plugin.FireRateRange?.Value ?? 25;
        overClockFireRate += incrementValue;
        if (overClockFireRate > MaxFireRate)
            overClockFireRate = MinFireRate;

        DisplayFireRateNotification(overClockFireRate);

        // Registers the new fire rate for current weapon
        if (!string.IsNullOrEmpty(weaponId))
        {
            CacheObject.RegisterFireRate(weaponId, overClockFireRate);
            BatchLogger.Info(
                $"[AnimationOverClock] Fire rate increase registered: {overClockFireRate} RPM for {weaponId}");
        }
    }
}