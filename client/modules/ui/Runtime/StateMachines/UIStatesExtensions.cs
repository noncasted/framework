using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public static class UIStatesExtensions
    {
        public static IUIStateHandle EnterChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.CreateChild(lifetime, parent, state).TryEnter();
        }

        // The state is a child of Base while the lifetime lives: its constraints act until the lifetime ends.
        // For a window of a scope that is not a UI state itself (the pause menu, the shop, the chat line).
        public static IUIStateHandle EnterWhile(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState state)
        {
            return stateMachine.CreateChild(lifetime, stateMachine.Base, state);
        }

        // Every Process* gives the caller no handle, so the state ends with the call, also when it has no enter handler.
        public static UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return ExitAfter(stateMachine.CreateChild(lifetime, parent, state), static handle => handle.TryProcess());
        }

        public static UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            return ExitAfter(stateMachine.CreateChild(lifetime, parent, state), action);
        }

        public static UniTask<T> ProcessChild<T>(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask<T>> action)
        {
            return ExitAfter(stateMachine.CreateChild(lifetime, parent, state), action);
        }

        public static UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return ExitAfter(stateMachine.CreateStackChild(lifetime, parent, state), static handle => handle.TryProcess());
        }

        public static UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            return ExitAfter(stateMachine.CreateStackChild(lifetime, parent, state), action);
        }

        public static UniTask<T> ProcessStack<T>(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask<T>> action)
        {
            return ExitAfter(stateMachine.CreateStackChild(lifetime, parent, state), action);
        }

        public static UniTask Process(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.ProcessChild(lifetime, parent, state);
        }

        // In finally: a window whose action throws would keep its constraints (the game input off) forever.
        private static async UniTask ExitAfter(IUIStateHandle handle, Func<IUIStateHandle, UniTask> action)
        {
            try
            {
                await action.Invoke(handle);
            }
            finally
            {
                handle.Exit();
            }
        }

        private static async UniTask<T> ExitAfter<T>(IUIStateHandle handle, Func<IUIStateHandle, UniTask<T>> action)
        {
            try
            {
                return await action.Invoke(handle);
            }
            finally
            {
                handle.Exit();
            }
        }

        public static async UniTask TryProcess(this IUIStateHandle handle)
        {
            try
            {
                switch (handle.State)
                {
                    case IUIStateEnterHandler handler:
                        handler.OnEntered(handle);
                        await handle.Completion.Task;
                        break;
                    case IUIStateAsyncEnterHandler handler:
                        await handler.OnEntered(handle);
                        break;
                }
            }
            finally
            {
                // A state without an enter handler stays: its owner exits it.
                if (handle.State is IUIStateEnterHandler or IUIStateAsyncEnterHandler)
                    handle.Exit();
            }
        }

        public static IUIStateHandle TryEnter(this IUIStateHandle handle)
        {
            switch (handle.State)
            {
                case IUIStateEnterHandler handler:
                    // The state with its constraints does not stay after an error; the caller gets the error.
                    try
                    {
                        handler.OnEntered(handle);
                    }
                    catch
                    {
                        handle.Exit();
                        throw;
                    }

                    break;
                case IUIStateAsyncEnterHandler handler:
                    RunEntered(handler, handle).NoAwait();
                    break;
            }

            return handle;
        }

        // Nobody awaits the enter: the state with its constraints does not stay after an error, and the error is reported.
        private static async UniTask RunEntered(IUIStateAsyncEnterHandler handler, IUIStateHandle handle)
        {
            try
            {
                await handler.OnEntered(handle);
            }
            catch
            {
                handle.Exit();
                throw;
            }
        }
    }
}