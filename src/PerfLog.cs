// Battle profiler: reports the game's own per-section timers. Write-up: notes/performance.md

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BugFixMod
{
    // ------------------------------------------------------------------------------
    // The game times about 35 sections of its per-frame work (Tools.UpdateCounterInMs into
    // GameVars.debug_performancesingle, always on), but nothing ever reads them: the debug
    // overlay prints a different array (debug_performance) that is never filled. This logs the
    // timers to LogOutput.log every PerfLogSeconds while enabled, as average ms per frame next to
    // the average frame time, so slow battles can be profiled without tools.
    // Labels name the call that closes each timed section (Regiment.Update unless noted).
    // ------------------------------------------------------------------------------
    class PerfLogger : MonoBehaviour
    {
        static readonly Dictionary<int, string> Labels = new Dictionary<int, string>
        {
            {0,"cover search"},{1,"symbol colour"},{2,"fleet terrain"},{3,"reattach/take guns"},{4,"movement (RegimentMove)"},
            {5,"last positions"},{6,"sprite positions"},{7,"sprite animation"},{8,"path arrows + sound"},{9,"dust + sprite meshes"},
            {10,"block parenting + sprite orientation"},{11,"final-waypoint checks"},{12,"waypoints"},{13,"unit targets"},
            {14,"sprite pos/selection"},{15,"missing orders"},{16,"unit ranges (UpdateUnitRangeFast)"},{17,"slow-cycle checks"},
            {18,"group status"},{19,"firing and reloading"},{20,"engagements/melees"},{21,"auto targets"},{22,"quick info"},
            {23,"path distances"},{24,"all-routed check"},{25,"fire range"},{26,"scout targets"},{27,"flying battery"},
            {28,"commander speed"},{29,"fire blocks"},{30,"movement mode"},{33,"safety zone"},{34,"unit link"},
            {35,"process orders"},{36,"detachment mode"},
        };

        float[] prev;
        float frameTime, elapsed;
        int frames;

        void Update()
        {
            if (!Cfg.PerfLog.Value || GameVars.debug_performancesingle == null) { prev = null; return; }
            var cur = GameVars.debug_performancesingle;
            if (prev == null || prev.Length != cur.Length) { prev = (float[])cur.Clone(); frames = 0; frameTime = 0f; elapsed = 0f; return; }
            frames++;
            frameTime += Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < Mathf.Max(2f, Cfg.PerfLogSeconds.Value)) return;

            var rows = new List<KeyValuePair<int, float>>();
            float sum = 0f;
            for (int i = 0; i < cur.Length; i++)
            {
                float ms = (cur[i] - prev[i]) / Mathf.Max(1, frames);
                if (ms > 0.005f) { rows.Add(new KeyValuePair<int, float>(i, ms)); sum += ms; }
            }
            if (rows.Count > 0)
            {
                int units = BattleUnits.completeunitlist != null ? BattleUnits.completeunitlist.Count : 0;
                var sb = new System.Text.StringBuilder();
                sb.Append("Perf: ").Append((frameTime * 1000f / frames).ToString("0.0")).Append(" ms/frame (")
                  .Append((frames / Mathf.Max(0.001f, frameTime)).ToString("0")).Append(" fps), units ").Append(units)
                  .Append(", game speed ").Append(Time.timeScale.ToString("0.#"))
                  .Append(", timed script work ").Append(sum.ToString("0.00")).Append(" ms/frame:");
                foreach (var r in rows.OrderByDescending(r => r.Value).Take(12))
                    sb.Append("\n   ").Append(r.Value.ToString("0.00")).Append(" ms  [").Append(r.Key).Append("] ")
                      .Append(Labels.TryGetValue(r.Key, out var l) ? l : "?");
                BugFixPlugin.Log.LogInfo(sb.ToString());
            }
            prev = (float[])cur.Clone();
            frames = 0; frameTime = 0f; elapsed = 0f;
        }
    }
}
