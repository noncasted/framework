using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Internal
{
    internal static class GeneratedScopeDisposeErrors
    {
        // Rethrows what a generated Dispose collected: one error as it is, several together.
        public static void Throw(List<Exception> errors)
        {
            if (errors == null)
                return;

            if (errors.Count == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();

            throw new AggregateException("Several services of the scope failed to dispose.", errors);
        }
    }
}
