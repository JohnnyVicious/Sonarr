using System;
using DryIoc;
using NLog;

namespace NzbDrone.Common.Instrumentation.Extensions
{
    [CLSCompliant(false)]
    public static class CompositionExtensions
    {
        public static IContainer AddNzbDroneLogger(this IContainer container)
        {
            container.Register(Made.Of<Logger>(() => LogManager.GetLogger(Arg.Index<string>(0)), r => r.Parent.ImplementationType.Name.ToString()), reuse: Reuse.Transient);
            return container;
        }
    }
}
