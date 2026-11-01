using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Internal.Tests
{
    public class OptionsAndStorageTests
    {
        [Test]
        public void SocketUrl_ReplacesOnlyTheScheme()
        {
            const string local = "http://localhost:5003/http";
            const string production = "https://api.example.com/https";

            Assert.AreEqual("ws://localhost:5003/http", Backend(BackendEnvironment.Local, local, production).SocketUrl);
            Assert.AreEqual("wss://api.example.com/https", Backend(BackendEnvironment.Production, local, production).SocketUrl);
        }

        [Test]
        public void SocketUrl_UnknownScheme_Throws()
        {
            var options = Backend(BackendEnvironment.Local, "localhost:5003", "");

            Assert.Throws<InvalidOperationException>(() => _ = options.SocketUrl);
        }

        private static BackendOptions Backend(BackendEnvironment environment, string local, string production)
        {
            return JsonUtility.FromJson<BackendOptions>(
                $"{{\"_environment\":{(int)environment},\"_localApiUrl\":\"{local}\",\"_productionApiUrl\":\"{production}\"}}");
        }

        [Test]
        public void LocalStorageKey_NameWithAnEnvironmentSuffix_Throws()
        {
            // It would be the key of the record of that environment.
            Assert.Throws<ArgumentException>(() => LocalStorageKeys.Get("Auth.Guest.Production"));
            Assert.Throws<ArgumentException>(() => LocalStorageKeys.Get("Auth.Guest.Local"));

            Assert.AreNotEqual(LocalStorageKeys.Get("Auth.Guest"), LocalStorageKeys.Get("Auth.Guest", BackendEnvironment.Local));
        }

        [Test]
        public void ProfilerTraceLoad_MissingFile_Throws()
        {
            var path = Path.Combine(Path.GetTempPath(), $"trace_missing_{Guid.NewGuid():N}.json");

            Assert.Throws<FileNotFoundException>(() => ProfilerTraceStorage.Load(path));
        }

        [Test]
        public void ProfilerTraceSave_Failure_Throws()
        {
            Assert.Throws<NullReferenceException>(() => ProfilerTraceStorage.Save(null));
        }
    }
}
