using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Global.Publisher.Telegram.Tests
{
    // The debug API signs the init data with the token from the settings file the project names
    // (TelegramEditorOptions), not from a path of one project.
    public class TelegramDebugAPITests
    {
        private const string Token = "123:test-token";

        [Test]
        public void GetInitData_SignedWithTokenOfOptionsFile()
        {
            var projectFolder = Path.GetDirectoryName(Application.dataPath);
            var fileName = $"telegram-test-{Guid.NewGuid():N}.json";
            var path = Path.Combine(projectFolder, "Temp", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{\"TelegramBotToken\": \"" + Token + "\"}");

            try
            {
                var api = new TelegramDebugAPI(new TelegramEditorOptions(Path.Combine("Temp", fileName)));
                var initData = api.GetInitData();

                Assert.AreEqual(Hash(initData), Read(initData, "hash"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        // The data check string of the Bot API: every field but the hash, sorted by key, "key=value" joined by \n.
        private static string Hash(string initData)
        {
            var authDate = Read(initData, "auth_date");
            var queryId = Uri.UnescapeDataString(Read(initData, "query_id"));
            var user = Uri.UnescapeDataString(Read(initData, "user"));
            var dataCheckString = $"auth_date={authDate}\nquery_id={queryId}\nuser={user}";

            using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
            var secret = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(Token));

            using var hashHmac = new HMACSHA256(secret);
            var hash = hashHmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Read(string initData, string key)
        {
            foreach (var pair in initData.Split('&'))
            {
                if (pair.StartsWith(key + "=", StringComparison.Ordinal) == true)
                    return pair.Substring(key.Length + 1);
            }

            throw new InvalidOperationException($"No {key} in {initData}");
        }
    }
}
