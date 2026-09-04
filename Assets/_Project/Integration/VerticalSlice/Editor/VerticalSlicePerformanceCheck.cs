#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectHive.AI.Mob;
using ProjectHive.Core.Runtime;
using ProjectHive.Gameplay.Raid;
using UnityEditor;
using UnityEngine;

namespace ProjectHive.Editor.Integration
{
    public static class VerticalSlicePerformanceCheck
    {
        private static readonly List<float> Frames = new List<float>(5000);
        private static double warmupUntil, finishAt;
        private static int lastFrame, drawCalls, triangles;
        private static long drawSum, triangleSum;
        private static bool pauseAtEnd;
        private static string label;
        public static string LastResult { get; private set; } = "Not started";

        public static void Begin(string sampleLabel, float seconds = 20f, bool pauseAfter = true)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            Frames.Clear();
            drawSum = triangleSum = 0;
            warmupUntil = EditorApplication.timeSinceStartup + 4d;
            finishAt = warmupUntil + seconds;
            lastFrame = -1;
            label = sampleLabel;
            pauseAtEnd = pauseAfter;
            LastResult = "Sampling " + label;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                LastResult = "Cancelled: Play Mode ended";
                return;
            }
            if (EditorApplication.isPaused) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < warmupUntil || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            Frames.Add(Time.unscaledDeltaTime * 1000f);
            drawCalls = UnityStats.drawCalls;
            triangles = UnityStats.triangles;
            drawSum += drawCalls;
            triangleSum += triangles;
            if (now < finishAt) return;
            EditorApplication.update -= Tick;
            float[] sorted = Frames.OrderBy(f => f).ToArray();
            float mean = Frames.Average();
            var enemies = UnityEngine.Object.FindObjectsByType<BreckenAI>(FindObjectsSortMode.None);
            var spawner = UnityEngine.Object.FindFirstObjectByType<PrototypeEnemyActivator>();
            var controller = UnityEngine.Object.FindFirstObjectByType<VerticalSliceRaidController>();
            LastResult = $"{label}: frames={Frames.Count}, meanMs={mean:F2}, meanFPS={1000f / mean:F1}, " +
                $"p95Ms={sorted[Mathf.Clamp((int)(sorted.Length * 0.95f), 0, sorted.Length - 1)]:F2}, maxMs={sorted[sorted.Length - 1]:F2}, " +
                $"drawCallsMean={drawSum / Frames.Count}, trianglesMean={triangleSum / Frames.Count}, " +
                $"enemies={enemies.Length}, activeAI={enemies.Count(e => e.isActiveAndEnabled)}, " +
                $"ticksRegistered={(RuntimeCoordinator.Instance != null ? RuntimeCoordinator.Instance.RegisteredCount : 0)}, " +
                $"spawned={(spawner != null ? spawner.ActivatedEnemyCount : 0)}, raidEnded={(controller != null && controller.HasEnded)}, " +
                $"resolution={Screen.width}x{Screen.height}, vSync={QualitySettings.vSyncCount}, targetFPS={Application.targetFrameRate}";
            Directory.CreateDirectory("Temp/TeamReview/20260905_lod_districts");
            File.WriteAllText("Temp/TeamReview/20260905_lod_districts/Performance_" + label + ".txt", LastResult);
            Debug.Log("[DistrictPerformance] " + LastResult);
            if (pauseAtEnd) EditorApplication.isPaused = true;
        }
    }
}
#endif
