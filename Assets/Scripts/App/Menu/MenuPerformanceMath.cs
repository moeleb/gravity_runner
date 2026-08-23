namespace GravityHalfDead
{
    internal static class MenuPerformanceMath
    {
        public static float Window(float value, float start, float end)
        {
            if (value <= start || value >= end)
                return 0f;
            var normalized = (value - start) / (end - start);
            const float edge = 0.16f;
            if (normalized < edge) return Smooth(normalized / edge);
            if (normalized > 1f - edge) return Smooth((1f - normalized) / edge);
            return 1f;
        }

        public static float Smooth(float value) => value * value * (3f - 2f * value);
    }
}
