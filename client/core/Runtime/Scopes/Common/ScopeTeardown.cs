using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The steps of tearing a scope down: a failing step does not stop the others, the errors come out at the end.
    internal sealed class ScopeTeardown
    {
        private List<Exception> _errors;

        public void Step(Action step)
        {
            try
            {
                step();
            }
            catch (Exception exception)
            {
                Add(exception);
            }
        }

        public async UniTask StepAwaited(Func<UniTask> step)
        {
            try
            {
                await step();
            }
            catch (Exception exception)
            {
                Add(exception);
            }
        }

        public void ThrowErrors()
        {
            GeneratedScopes.ThrowDisposeErrors(_errors);
        }

        // The teardown after a failed load: its errors go out together with the error of the load.
        public void ThrowWithLoadFailure(Exception failure)
        {
            if (_errors == null)
                return;

            _errors.Insert(0, failure);
            throw new AggregateException("Scope load failed, and its teardown failed too.", _errors);
        }

        private void Add(Exception exception)
        {
            _errors ??= new List<Exception>();
            _errors.Add(exception);
        }
    }
}
