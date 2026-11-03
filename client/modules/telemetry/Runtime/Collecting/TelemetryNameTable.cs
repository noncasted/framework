using System.Collections.Generic;

namespace Global.Telemetry
{
    // The list of the names of a session: a value in a sample goes by the index of its name, a new name takes the next
    // index and is sent once.
    public class TelemetryNameTable
    {
        public TelemetryNameTable(TelemetryNameGuard guard)
        {
            _guard = guard;
        }

        private readonly TelemetryNameGuard _guard;
        private readonly Dictionary<string, int> _indices = new();
        private readonly List<string> _names = new();
        private readonly List<string> _units = new();

        public int Count => _names.Count;

        // The names from here on were not sent yet.
        public int Written { get; private set; }

        public bool TryGetIndex(string name, string unit, out int index)
        {
            // A known name is a valid one: the check of the characters runs once per name.
            if (name != null && _indices.TryGetValue(name, out index))
                return _units[index] == unit || _guard.Reject($"The name '{name}' has the unit '{_units[index]}', not '{unit}'");

            index = -1;

            if (_guard.AcceptName(name) == false || _guard.AcceptUnit(unit) == false)
                return false;

            // The server rejects a whole batch over this limit.
            if (_names.Count >= TelemetryLimits.MaxNamesPerSession)
                return _guard.Reject($"The name '{name}': over {TelemetryLimits.MaxNamesPerSession} names in a session");

            index = _names.Count;
            _indices.Add(name, index);
            _names.Add(name);
            _units.Add(unit);

            return true;
        }

        // A new session: its names take the slots from the first one and are sent again.
        public void Clear()
        {
            _indices.Clear();
            _names.Clear();
            _units.Clear();
            Written = 0;
        }

        public string NameAt(int index)
        {
            return _names[index];
        }

        public string UnitAt(int index)
        {
            return _units[index];
        }

        public void MarkAllWritten()
        {
            Written = _names.Count;
        }
    }
}
