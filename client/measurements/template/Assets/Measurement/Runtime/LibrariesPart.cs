using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using UnityEngine;

namespace Measurement
{
    /// <summary>
    ///     The managed code a game runs besides the engine: the reactive primitives of the framework, UniTask,
    ///     MemoryPack and the parts of the base class libraries the games use (collections, LINQ, strings and
    ///     number formatting, exceptions, tasks). This is the part the .NET version of the BCL changes.
    /// </summary>
    public sealed class LibrariesPart : MonoBehaviour, IMeasurementPart
    {
        public string Name => "libraries";

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            var reactive = await RunReactive(cancellation);
            var packet = BuildPacket();
            var bytes = MemoryPackSerializer.Serialize(packet);
            var restored = MemoryPackSerializer.Deserialize<SnapshotPacket>(bytes);

            if (restored.Id != packet.Id || restored.Points.Count != packet.Points.Count)
                throw new InvalidOperationException("MemoryPack round trip changed the packet");

            var text = FormatText(restored);
            var caught = ThrowAndCatch();
            var awaited = await AwaitTask(cancellation);

            return $"reactive {reactive}, packet {bytes.Length} B, text {text.Length} chars, caught {caught}, task {awaited}";
        }

        private static async UniTask<int> RunReactive(CancellationToken cancellation)
        {
            var lifetime = new Lifetime();
            var events = new EventSource<int>();
            var property = new ViewableProperty<int>(0);
            var values = new ViewableList<int>();
            var received = 0;

            events.Advise(lifetime, value => received += value);
            property.View(lifetime, value => received += value);
            values.View(lifetime, (_, value) => received += value);

            for (var i = 1; i <= 10; i++)
            {
                events.Invoke(i);
                property.Set(i);
                values.Add(i);
                await UniTask.Yield(cancellation);
            }

            lifetime.Terminate();
            events.Invoke(1000);

            return received;
        }

        private static SnapshotPacket BuildPacket()
        {
            var random = new System.Random(42);
            var scores = Enumerable.Range(0, 200)
                                   .Select(i => (Team: $"team-{i % 7}", Points: random.Next(0, 100)))
                                   .ToList();

            var points = scores.GroupBy(score => score.Team)
                               .ToDictionary(group => group.Key, group => group.Sum(score => score.Points));

            var order = points.OrderByDescending(pair => pair.Value)
                              .Select(pair => int.Parse(pair.Key.Substring(5), CultureInfo.InvariantCulture))
                              .ToArray();

            return new SnapshotPacket
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Duration = TimeSpan.FromSeconds(random.NextDouble() * 100),
                Text = string.Join(",", points.Keys),
                Order = order,
                Samples = scores.Select(score => score.Points / 3f).Where(value => value > 10f).ToList(),
                Points = points,
                Winner = order.FirstOrDefault()
            };
        }

        private static string FormatText(SnapshotPacket packet)
        {
            var builder = new StringBuilder();
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0:N2} s ", packet.Duration.TotalSeconds);
            builder.Append(packet.Created.ToString("O", CultureInfo.InvariantCulture));

            foreach (var pair in packet.Points.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                builder.Append($" {pair.Key}={pair.Value:D3}");

            var average = packet.Samples.Count > 0 ? packet.Samples.Average() : 0f;
            builder.Append(' ').Append(average.ToString("F3", CultureInfo.InvariantCulture));

            var parsed = double.Parse("12.5", CultureInfo.InvariantCulture) + float.Parse("0.5", CultureInfo.InvariantCulture);
            builder.Append(' ').Append(parsed);

            var utf8 = Encoding.UTF8.GetBytes(builder.ToString());
            var unique = new HashSet<byte>(utf8);

            return $"{Encoding.UTF8.GetString(utf8)} [{unique.Count}]";
        }

        private static string ThrowAndCatch()
        {
            try
            {
                throw new InvalidOperationException("expected");
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        // A Task completed from a UniTask continuation: the TPL machinery without the thread pool, which the
        // single-threaded web player does not have.
        private static async UniTask<int> AwaitTask(CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<int>();
            CompleteLater(completion, cancellation).Forget();

            return await completion.Task;
        }

        private static async UniTaskVoid CompleteLater(TaskCompletionSource<int> completion, CancellationToken cancellation)
        {
            await UniTask.DelayFrame(2, cancellationToken: cancellation);
            completion.SetResult(7);
        }
    }
}
