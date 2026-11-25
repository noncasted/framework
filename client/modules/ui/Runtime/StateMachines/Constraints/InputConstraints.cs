using System;

namespace Global.UI
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
}
