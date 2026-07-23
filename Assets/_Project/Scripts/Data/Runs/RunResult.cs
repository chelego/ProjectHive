using System;
using System.Collections.Generic;
using ProjectHive.Data.Items;
using UnityEngine;

namespace ProjectHive.Data.Runs
{
    public enum RunOutcome
    {
        None = 0,
        Extracted = 1,
        Dead = 2,
        TimeExpired = 3,
        Abandoned = 4
    }

    [Serializable]
    public sealed class RunResult
    {
        [SerializeField] private RunOutcome outcome;
        [SerializeField] private string mapId;
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField] private List<ItemStack> recoveredItems = new List<ItemStack>();

        public RunResult(
            RunOutcome runOutcome,
            string runMapId,
            float duration,
            IEnumerable<ItemStack> items = null)
        {
            outcome = runOutcome;
            mapId = runMapId ?? string.Empty;
            durationSeconds = Mathf.Max(0f, duration);
            recoveredItems = items == null ? new List<ItemStack>() : new List<ItemStack>(items);
        }

        public RunOutcome Outcome => outcome;
        public string MapId => mapId;
        public float DurationSeconds => durationSeconds;
        public IReadOnlyList<ItemStack> RecoveredItems => recoveredItems;
    }
}
