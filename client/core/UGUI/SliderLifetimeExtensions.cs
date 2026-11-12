using UnityEngine.UI;

namespace Internal
{
    public static class SliderLifetimeExtensions
    {
        public static ILifetimedValue<float> ToLifetimedValue(this Slider source, IReadOnlyLifetime lifetime)
        {
            return source.onValueChanged.ToLifetimedValue(source.value, lifetime);
        }
    }
}
