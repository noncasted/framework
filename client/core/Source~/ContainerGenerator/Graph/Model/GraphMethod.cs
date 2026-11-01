using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphMethod {
        public string Id = "";
        public string OriginId = "";
        public string ParentHint = "";
        public string ParentId = "";
        public string ParentAssembly = "";
        public string AssemblyName = "";
        public bool ParentUnresolved;
        public bool IsRoot;
        public string File = "";
        public int Line;
        public string ViewType = "";
        public string Variant = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<string> Calls = new List<string>();
        public List<int> CallOrdinals = new List<int>();
        public List<string> CallTypeArguments = new List<string>();
        public List<string> CallAsServices = new List<string>();
        public List<string> CallHoles = new List<string>();
        // The types of the WithParameter chained on the call, joined with ';' (see GraphRegistration.ParameterTypes).
        public List<string> CallParameters = new List<string>();
        public int ReturnedOrdinal = -1;

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
