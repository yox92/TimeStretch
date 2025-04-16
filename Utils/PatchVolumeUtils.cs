using System;
using System.Linq.Expressions;

namespace TimeStretch.Utils
{
    public static class PatchVolumeUtils
    {
        private static readonly Func<BetterSource, float> GetVolume;
        private static readonly Action<BetterSource, float> SetVolume;

        static PatchVolumeUtils()
        {
            var instance = Expression.Parameter(typeof(BetterSource), "instance");
            var value = Expression.Parameter(typeof(float), "value");
            var prop = typeof(BetterSource).GetProperty("BaseVolume");

            if (prop != null)
            {
                GetVolume = Expression.Lambda<Func<BetterSource, float>>(
                    Expression.Property(instance, prop), instance).Compile();

                SetVolume = Expression.Lambda<Action<BetterSource, float>>(
                    Expression.Assign(Expression.Property(instance, prop), value), instance, value).Compile();
            }
            else
            {
                GetVolume = _ => 1f;
                SetVolume = (_, __) => { };
            }
        }

        public static float GetBaseVolume(BetterSource source) => GetVolume(source);
        public static void SetBaseVolume(BetterSource source, float value) => SetVolume(source, value);
    }
}