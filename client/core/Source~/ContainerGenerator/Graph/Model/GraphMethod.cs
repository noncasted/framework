using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphMethod {
        private string _assemblyName = "";
        private bool _isRoot;
        private string _file = "";
        private int _line;
        private string _viewType = "";

        public string Id { get; init; } = "";
        public string OriginId { get; init; } = "";
        public string ParentHint { get; private set; } = "";
        public string ParentId { get; private set; } = "";
        public string ParentAssembly { get; private set; } = "";
        public string AssemblyName { get => _assemblyName; init => _assemblyName = value; }
        public bool ParentUnresolved { get; private set; }
        public bool IsRoot { get => _isRoot; init => _isRoot = value; }
        public string File { get => _file; init => _file = value; }
        public int Line { get => _line; init => _line = value; }
        public string ViewType { get => _viewType; init => _viewType = value; }
        public string Variant { get; init; } = "";
        public List<GraphRegistration> Registrations { get; } = new List<GraphRegistration>();
        public List<string> Calls { get; } = new List<string>();
        public List<int> CallOrdinals { get; } = new List<int>();
        public List<string> CallTypeArguments { get; } = new List<string>();
        public List<string> CallAsServices { get; } = new List<string>();
        public List<string> CallHoles { get; } = new List<string>();
        // The types of the WithParameter chained on the call, joined with ';' (see GraphRegistration.ParameterTypes).
        public List<string> CallParameters { get; } = new List<string>();
        public int ReturnedOrdinal { get; private set; } = -1;

        public void SetRoot(bool isRoot) {
            _isRoot = isRoot;
        }

        public void SetViewType(string viewType) {
            _viewType = viewType;
        }

        public void SetAssemblyName(string assemblyName) {
            _assemblyName = assemblyName;
        }

        public void Locate(string file, int line) {
            _file = file;
            _line = line;
        }

        public void SetParent(string hint, string assembly) {
            ParentHint = hint;
            ParentAssembly = assembly;
        }

        public void SetParentHint(string hint) {
            ParentHint = hint;
        }

        public void SetParentId(string id) {
            ParentId = id;
        }

        public void MarkParentUnresolved() {
            ParentUnresolved = true;
        }

        public void SetReturnedOrdinal(int ordinal) {
            ReturnedOrdinal = ordinal;
        }

        // The same method read from another manifest: only what this one does not know yet.
        public void FillMissingFrom(GraphMethod other) {
            if (ReturnedOrdinal < 0)
                ReturnedOrdinal = other.ReturnedOrdinal;
            if (string.IsNullOrEmpty(File))
                _file = other.File;
            if (Line == 0)
                _line = other.Line;
        }

        public void AddCall(
            string id,
            int ordinal,
            string typeArguments = "",
            string asServices = "",
            string hole = "") {
            if (string.IsNullOrEmpty(id))
                return;

            Calls.Add(id);
            CallOrdinals.Add(ordinal);
            CallTypeArguments.Add(typeArguments ?? "");
            CallAsServices.Add(asServices ?? "");
            CallHoles.Add(hole ?? "");
            CallParameters.Add("");
        }

        public void PadCallLists() {
            while (CallOrdinals.Count < Calls.Count)
                CallOrdinals.Add(int.MaxValue);
            while (CallTypeArguments.Count < Calls.Count)
                CallTypeArguments.Add("");
            while (CallAsServices.Count < Calls.Count)
                CallAsServices.Add("");
            while (CallHoles.Count < Calls.Count)
                CallHoles.Add("");
            while (CallParameters.Count < Calls.Count)
                CallParameters.Add("");
        }

        public string CallTypesAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallTypeArguments.Count)
                return "";
            return CallTypeArguments[index];
        }

        public string CallAsAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallAsServices.Count)
                return "";
            return CallAsServices[index];
        }

        public string CallHoleAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallHoles.Count)
                return "";
            return CallHoles[index];
        }

        public string CallParametersAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallParameters.Count)
                return "";
            return CallParameters[index];
        }

        public void AddParameterToCall(int index, string type) {
            PadCallLists();
            if (index < 0 || index >= CallParameters.Count || string.IsNullOrEmpty(type))
                return;
            CallParameters[index] = string.IsNullOrEmpty(CallParameters[index]) ? type : CallParameters[index] + ";" + type;
        }

        public void AddAsToCall(int index, string service) {
            PadCallLists();
            if (index < 0 || index >= CallAsServices.Count)
                return;
            if (string.IsNullOrEmpty(service))
                return;
            if (string.IsNullOrEmpty(CallAsServices[index])) {
                CallAsServices[index] = service;
                return;
            }

            if (CallAsServices[index].IndexOf(service, System.StringComparison.Ordinal) >= 0)
                return;
            CallAsServices[index] = CallAsServices[index] + ";" + service;
        }

        public void AddHoleToCall(int index, string hole) {
            PadCallLists();
            if (index < 0 || index >= CallHoles.Count)
                return;
            if (string.IsNullOrEmpty(hole))
                return;
            if (string.IsNullOrEmpty(CallHoles[index])) {
                CallHoles[index] = hole;
                return;
            }

            CallHoles[index] = CallHoles[index] + "; " + hole;
        }
    }
}
