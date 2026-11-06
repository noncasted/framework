using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Measurement
{
    /// <summary>
    ///     Root of the measured scene: runs every part in order and logs the result line the smoke run waits for
    ///     (<see cref="ReadyMarker" /> or <see cref="FailedMarker" />).
    /// </summary>
    public sealed class MeasurementRun : MonoBehaviour
    {
        public const string ReadyMarker = "[measurement] ready";
        public const string FailedMarker = "[measurement] failed";
        public const string PartMarker = "[measurement] part";

        [SerializeField] private List<MonoBehaviour> _parts = new();

        public void AddPart(MonoBehaviour part)
        {
            if (part is IMeasurementPart == false)
                throw new ArgumentException($"{part.GetType().Name} is not an {nameof(IMeasurementPart)}");

            _parts.Add(part);
        }

        private void Start()
        {
            RunParts().Forget();
        }

        private async UniTaskVoid RunParts()
        {
            var cancellation = destroyCancellationToken;
            var results = new List<string>();

            // The top of a task nobody awaits: the failure is reported to the smoke run instead of being lost.
            try
            {
                foreach (var behaviour in _parts)
                {
                    var part = (IMeasurementPart)behaviour;
                    Debug.Log($"{PartMarker} {part.Name}");
                    var result = await part.Run(cancellation);
                    results.Add($"{part.Name}: {result}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"{FailedMarker}: {exception}");
                return;
            }

            Debug.Log($"{ReadyMarker} {string.Join("; ", results)}");
        }
    }
}
