using System;

namespace NzbDrone.Common.TPL
{
    [CLSCompliant(false)]
    public interface IDebounceManager
    {
        Debouncer CreateDebouncer(Action action, TimeSpan debounceDuration);
    }

    [CLSCompliant(false)]
    public class DebounceManager : IDebounceManager
    {
        public Debouncer CreateDebouncer(Action action, TimeSpan debounceDuration)
        {
            return new Debouncer(action, debounceDuration);
        }
    }
}
