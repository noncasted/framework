// NOTE: generic code, do not use UnityEngine here
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Unity.Web.Stripping.Editor
{
    /// <summary>
    /// Class <b>WasmOpt</b> runs wasm-opt command line tool to optimize WASM binaries.
    /// </summary>
    class WasmOpt
    {
        /// <summary>
        /// Options for running the wasm-opt --instrument-functions pass
        /// </summary>
        public class InstrumentFunctionsOptions
        {
            /// <summary>
            /// Path to the instrumentation config file
            /// </summary>
            public string InstrumentationConfigFile { get; set; } = string.Empty;

            /// <summary>
            /// The name of the import module in the WebAssembly file.
            /// Default: env
            /// </summary>
            public string WasmImportModule { get; set; } = "env";

            /// <summary>
            /// Path to where the label map file will be stored.
            /// Default: labels.json
            /// </summary>
            public string LabelsFile { get; set; } = "labels.json";

            /// <summary>
            /// Path to where the function id map will be stored.
            /// Default: functions.json
            /// </summary>
            public string FunctionsMapFile { get; set; } = "functions.json";
        }

        /// <summary>
        /// Path to Emscripten SDK
        /// </summary>
        public string EmscriptenSdkPath { get; set; } = string.Empty;

        /// <summary>
        /// Log executed commands and their output to the console.
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Emit WebAssembly as text format.
        /// </summary>
        public bool EmitWat { get; set; } = false;

        /// <summary>
        /// Keep debug information during optimization (function names).
        /// </summary>
        public bool KeepDebugInformation { get; set; } = false;

        /// <summary>
        /// Enables WebAssembly 2023 features (SIMD, exception handling).
        /// </summary>
        public bool EnableWasm2023 { get; set; } = false;

        /// <summary>
        /// Enables support for WebAssembly threads.
        /// </summary>
        public bool EnableThreadSupport { get; set; } = false;

        /// <summary>
        /// Enables support for Wasm BigInt. This allows 64-Bit numbers in
        /// function interfaces callable from JavaScript.
        /// </summary>
        public bool EnableBigIntSupport { get; set; } = false;

        /// <summary>
        /// Enables support for Wasm64 (memory64). This is required when memory
        /// is configured above 4 GB, where pointers become 64-bit.
        /// </summary>
        public bool EnableWasm64 { get; set; } = false;

        /// <summary>
        /// Enable if build was created with Emscripten 4 features.
        /// Emscripten 4 with WebAssembly 2023 enabled will use the WASM features:
        /// - bulk-memory
        /// - bulk-memory-opt
        /// - nontrapping-float-to-int-conversions
        /// </summary>
        public bool EnableEmscripten4Features { get; set; } = false;

        /// <summary>
        /// If the build was created as a development build (`UnityEditor.EditorUserBuildSettings.development`).
        /// </summary>
        public bool Development { get; set; } = false;

        /// <summary>
        /// The code optimization setting used for the build.
        /// </summary>
        public string CodeOptimization { get; set; } = "";

        /// <summary>
        /// Log output. Default: System.Console.Out
        /// </summary>
        public TextWriter Log = Console.Out;

        /// <summary>
        /// Error log output. Default: System.Console.Error
        /// </summary>
        public TextWriter ErrorLog = Console.Error;

        private string WasmOptPath
        {
            get
            {
                return Path.Combine(EmscriptenSdkPath, "binaryen", "bin", $"wasm-opt{CommandLineUtils.HostPlatformExe}");
            }
        }

        private string EmitWatArg => EmitWat ? " --emit-text" : "";
        private string KeepDebugInformationArg => KeepDebugInformation ? " -g" : "";
        private string EnableWasm2023Arg => EnableWasm2023 ? " --enable-simd --enable-exception-handling" : "";
        private string EnableThreadsArg => EnableThreadSupport ? " --enable-threads" : "";
        private string EnableWasm64Arg => EnableWasm64 ? " --enable-memory64" : "";
        private string EnableEmscripten4FeaturesArg
        {
            get
            {
                if (EnableEmscripten4Features && EnableWasm2023)
                    return " --enable-bulk-memory --enable-bulk-memory-opt --enable-nontrapping-float-to-int -n";
                // Local patch: without --enable-exception-handling binaryen assumes calls never throw and -O3 drops
                // every try/catch of a -fwasm-exceptions build. Any exception (even one caught in C#, e.g.
                // OperationCanceledException of a cancelled search) crashes the player. With Wasm 2023 the flag
                // comes from EnableWasm2023Arg already.
                else if (EnableEmscripten4Features && !EnableWasm2023)
                    return " --enable-exception-handling -n";
                else
                    return "";
            }
        }

        private const string SkipDuplicateFunctionEliminationArg = " --skip-pass=duplicate-function-elimination";

        private string OptPassArgs
        {
            get
            {
                if (!EnableEmscripten4Features)
                    return "";

                // For Emscripten 4 we need to match the optimization settings in wasm-opt as used for the build
                // to avoid increase in WebAssembly size.
                if (Development)
                    return $" -O1{SkipDuplicateFunctionEliminationArg}";

                switch(CodeOptimization?.ToLowerInvariant() ?? "")
                {
                case "buildtimes":
                    return $" -O2{SkipDuplicateFunctionEliminationArg}";
                case "runtimespeed":
                case "runtimespeedlto":
                    return $" -O3{SkipDuplicateFunctionEliminationArg}";
                case "disksize":
                case "disksizelto":
                    return $" -Os{SkipDuplicateFunctionEliminationArg}";
                default:
                    return "";
                }
            }
        }

        // BUG in wasm-opt: The post-emscripten pass only works with BitInt support enabled
        private string PostEmscriptenArg => EnableBigIntSupport ? " --post-emscripten" : "";
        private Regex RemovingFunctionLogRegex = new Regex("removing (void|i32|i64|f32|f64|v128) function ");

        /// <summary>
        /// Removes a list of functions from a WASM binary. Functions are either completly removed(if not referenced) or replaced with an empty implementation.
        /// </summary>
        /// <param name="wasmFile">Path to the input WASM binary.</param>
        /// <param name="outputWasmFile">Path to the output WASM binary.</param>
        /// <param name="functionIdsFile">Path to a function ids file that contains functions that should be removed.</param>
        /// <returns>
        /// A list of function names that were stripped from the wasm file.
        /// This may be different than the functions in <paramref name="functionIdsFile"/> if functions are not present in the WASM file.
        /// </returns>
        public HashSet<string> RemoveFunctions(string wasmFile, string outputWasmFile, string functionIdsFile)
        {
            string stdout = "";
            string stderror = "";
            var options = new CommandLineUtils.CommandOptions();
            if (Verbose)
            {
                options.Log += (text) => Log.WriteLine(text);
                options.ErrorLog += (text) => ErrorLog.WriteLine(text);
            }

            CommandLineUtils.Execute(
                WasmOptPath,
                $"\"{wasmFile}\" -o \"{outputWasmFile}\" --remove-functions=\"@{functionIdsFile}\"{OptPassArgs}{EmitWatArg}{KeepDebugInformationArg}{EnableWasm2023Arg}{EnableThreadsArg}{EnableEmscripten4FeaturesArg}{EnableWasm64Arg}",
                out stdout,
                out stderror,
                options
            );


            // Workaound: wasm-opt logs to stderror instead of stdout
            return ParseRemoveFunctionOutput(stderror);
        }

        /// <summary>
        /// Remove debug information from WASM binary.
        /// </summary>
        /// <param name="wasmFile">Path to the input WASM binary.</param>
        /// <param name="outputWasmFile">Path to the output WASM binary.</param>
        public void RemoveDebugInformation(string wasmFile, string outputWasmFile)
        {
            string stdout = "";
            string stderror = "";
            var options = new CommandLineUtils.CommandOptions();
            if (Verbose)
            {
                options.Log += (text) => Log.WriteLine(text);
                options.ErrorLog += (text) => ErrorLog.WriteLine(text);
            }

            CommandLineUtils.Execute(
                WasmOptPath,
                $"\"{wasmFile}\" -o \"{outputWasmFile}\" {EmitWatArg}{EnableWasm2023Arg}{EnableThreadsArg}{EnableEmscripten4FeaturesArg}{EnableWasm64Arg}",
                out stdout,
                out stderror,
                options
            );
        }

        /// <summary>
        /// Run further optimization passes. This can potentially remove more unnused functions after stripping.
        /// </summary>
        /// <param name="wasmFile">Path to the input WASM binary.</param>
        /// <param name="outputWasmFile">Path to the output WASM binary.</param>
        public void Optimize(string wasmFile, string outputWasmFile)
        {
            var options = new CommandLineUtils.CommandOptions();
            if (Verbose)
            {
                options.Log += (text) => Log.WriteLine(text);
                options.ErrorLog += (text) => ErrorLog.WriteLine(text);
            }

            CommandLineUtils.Execute(
                WasmOptPath,
                $"\"{wasmFile}\" -o \"{outputWasmFile}\" -Oz --inlining-optimizing --dce --coalesce-locals --code-folding --code-pushing --const-hoisting --dae-optimizing --duplicate-function-elimination --local-cse --once-reduction --optimize-for-js --optimize-instructions --optimize-stack-ir{PostEmscriptenArg} --merge-blocks --merge-locals --merge-similar-functions --remove-unused-brs --remove-unused-names --remove-unused-module-elements --remove-unused-nonfunction-module-elements --reorder-locals --rse --traps-never-happen {EmitWatArg}{KeepDebugInformationArg}{EnableWasm2023Arg}{EnableThreadsArg}{EnableEmscripten4FeaturesArg}{EnableWasm64Arg}",
                options
            );
        }

        /// <summary>
        /// Run --instrument-functions pass which adds a call to a log function to each
        /// configured function with a user-definable label per function.
        /// </summary>
        /// <param name="wasmFile">Path to the input WASM binary.</param>
        /// <param name="outputWasmFile">Path to the output WASM binary.</param>
        /// <param name="opts">Additional options for the pass.</param>
        public void InstrumentFunctions(string wasmFile, string outputWasmFile, InstrumentFunctionsOptions opts)
        {
            var options = new CommandLineUtils.CommandOptions();
            if (Verbose)
            {
                options.Log += (text) => Log.WriteLine(text);
                options.ErrorLog += (text) => ErrorLog.WriteLine(text);
            }

            CommandLineUtils.Execute(
                WasmOptPath,
                $"\"{wasmFile}\" -o \"{outputWasmFile}\" --instrument-functions=@\"{opts.InstrumentationConfigFile}\" --pass-arg=instrument-functions-logger-function-module@{opts.WasmImportModule} --pass-arg=\"instrument-functions-labels-file@{opts.LabelsFile}\" --pass-arg=\"instrument-functions-map-file@{opts.FunctionsMapFile}\"{OptPassArgs}{EmitWatArg}{KeepDebugInformationArg}{EnableWasm2023Arg}{EnableThreadsArg}{EnableEmscripten4FeaturesArg}{EnableWasm64Arg}",
                options
            );
        }

        /// <summary>
        /// Run --print-function-map pass and parse result to get a map from
        /// function index within WASM to name of function.
        /// </summary>
        /// <param name="wasmFile">Path to the input WASM binary.</param>
        /// <returns>A map from function index to name of function.</returns>
        public Dictionary<long, string> GetFunctionMap(string wasmFile)
        {
            var functionMap = new Dictionary<long, string>();

            var options = new CommandLineUtils.CommandOptions();
            if (Verbose)
            {
                options.Log += (text) => Log.WriteLine(text);
                options.ErrorLog += (text) => ErrorLog.WriteLine(text);
            }

            // Store result in temporary file
            var tmpFile = Path.Combine(Path.GetTempPath(), $"tmp_function_map_{Path.GetRandomFileName()}.txt");
            try
            {
                CommandLineUtils.Execute(
                    WasmOptPath,
                    $"\"{wasmFile}\" --print-function-map --symbolmap=\"{tmpFile}\" --all-features",
                    options
                );

                // The function map file uses a format where function index and name are separated by ':'
                // and there is one function per line
                var lines = File.ReadLines(tmpFile);
                foreach(var line in lines)
                {
                    var components = line.Split(':');
                    if (components.Length != 2)
                        continue;

                    long functionIndex  = -1L;
                    if (!long.TryParse(components[0], out functionIndex))
                        continue;

                    functionMap[functionIndex] = components[1];
                }
            }
            finally
            {
                // Delete temporary file
                if (File.Exists(tmpFile))
                    File.Delete(tmpFile);
            }

            return functionMap;
        }

        private HashSet<string> ParseRemoveFunctionOutput(string output)
        {
            // Parse output to find stripped functions
            var StrippedFunctions = new HashSet<string>();
            var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (!line.StartsWith("removing "))
                    continue;

                var functionName = RemovingFunctionLogRegex
                    .Replace(line, "")
                    .Replace("\\28", "(")
                    .Replace("\\29", ")")
                    .Replace("\\20", " ")
                    .Replace("\\2c", ",");

                StrippedFunctions.Add(functionName);
            }

            return StrippedFunctions;
        }
    }
}
