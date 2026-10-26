using System;
using System.Collections.Generic;
using Global.UI;

namespace Global.Inputs
{
    // A kind of input that a visible UI state can disable (IUIConstraints.Input). The framework declares Game; a
    // project declares its own kinds the same way, as static readonly instances in its code (the input of an editor
    // mode, of a camera of its own). A kind is told by its instance, the name is for the messages only.
    public sealed class InputConstraints
    {
        public InputConstraints(string name)
        {
            if (string.IsNullOrEmpty(name) == true)
                throw new ArgumentException("[InputConstraints] A kind of input needs a name", nameof(name));

            Name = name;
        }

        // The game input: the local player moves, aims, fires.
        public static readonly InputConstraints Game = new("Game");

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    // Which kinds of input are allowed now. A visible UI state adds its constraints (IUIState.Constraints) and
    // removes them when it is hidden or exits; the kind is allowed while no state disables it.
    public interface IInputConstraintsStorage
    {
        // True - the input of this kind is allowed.
        bool this[InputConstraints key] { get; }

        void Add(IUIConstraints uiConstraints);
        void Remove(IUIConstraints uiConstraints);
    }

    public class InputConstraintsStorage : IInputConstraintsStorage
    {
        // Number of the visible states that disable each kind of input. The kinds are declared by the framework and
        // by the project, so the storage does not list them: a kind no state has disabled yet is not here.
        private readonly Dictionary<InputConstraints, int> _constraints = new();

        public bool this[InputConstraints key] => _constraints.TryGetValue(key, out var count) == false || count == 0;

        // IUIConstraints.Input: false - the state disables this kind of input, true - leaves it allowed.
        public void Add(IUIConstraints uiConstraints)
        {
            foreach (var (key, isAllowed) in uiConstraints.Input)
            {
                if (isAllowed == true)
                    continue;

                _constraints.TryGetValue(key, out var count);
                _constraints[key] = count + 1;
            }
        }

        public void Remove(IUIConstraints uiConstraints)
        {
            foreach (var (key, isAllowed) in uiConstraints.Input)
            {
                if (isAllowed == true)
                    continue;

                if (_constraints.TryGetValue(key, out var count) == false || count == 0)
                    throw new InvalidOperationException($"[InputConstraints] {key} is removed more times than added");

                _constraints[key] = count - 1;
            }
        }
    }
}