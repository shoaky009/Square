namespace Square.Controls.Animation;

/// <summary>缓动函数集合，将线性进度 [0,1] 映射为非线性进度。</summary>
public static class Easing
{
    /// <summary>线性缓动。</summary>
    public static float Linear(float t) => t;

    /// <summary>三次方缓入。</summary>
    public static float EaseIn(float t) => t * t * t;
    /// <summary>三次方缓出。</summary>
    public static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    /// <summary>三次方缓入缓出。</summary>
    public static float EaseInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;

    /// <summary>二次方缓入。</summary>
    public static float EaseInQuad(float t) => t * t;
    /// <summary>二次方缓出。</summary>
    public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    /// <summary>二次方缓入缓出。</summary>
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
}
