using System.Collections.Generic;
using Global.Inputs;

namespace Global.UI
{
    public interface IUIConstraints
    {
        // Kind of input -> whether it stays allowed while the state is visible (false disables it).
        IReadOnlyDictionary<InputConstraints, bool> Input { get; }
    }

    public class UIConstraints : IUIConstraints
    {
        public UIConstraints()
        {
            Input = new Dictionary<InputConstraints, bool>();
        }

        // Disables the given kind of input.
        public UIConstraints(InputConstraints input)
        {
            Input = new Dictionary<InputConstraints, bool>()
            {
                { input, false }
            };
        }

        public UIConstraints(IReadOnlyDictionary<InputConstraints, bool> input)
        {
            Input = input;
        }

        public IReadOnlyDictionary<InputConstraints, bool> Input { get; }

        public static UIConstraints Empty => new UIConstraints();
        // The game input (the local player: move, aim, fire) is disabled.
        public static UIConstraints Game => new UIConstraints(InputConstraints.Game);
    }
}