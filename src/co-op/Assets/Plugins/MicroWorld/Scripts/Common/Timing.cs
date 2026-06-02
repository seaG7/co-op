using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroWorldNS
{
    public class Timing
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        int checkPointCounter = 0;
        long prevCheckpoint = 0;
        string title;

        static Stack<Timing> stack = new Stack<Timing>();

        public static void Checkpoint(string title = null)
        {
            LogTime(title);
        }

        public static void Pause(string title = null)
        {
            if (stack.Count == 0)
                return;
            var timing = stack.Peek();
            if (title != null)
                LogTime(title);
            timing.sw.Stop();
        }

        public static void Resume()
        {
            if (stack.Count == 0)
                return;
            var timing = stack.Peek();
            timing.sw.Start();
        }

        public static void Start(string title = null)
        {
            var timing = new Timing() { title = title };
            stack.Push(timing);
            timing.sw.Start();
        }

        public static void Stop(string title = null)
        {
            if (stack.Count == 0)
                return;
            LogTime(title);
            stack.Pop().sw.Stop();
        }

        static void LogTime(string title = null)
        {
            if (stack.Count == 0)
                return;
            var timing = stack.Peek();
            title = title ?? timing.title;
            if (timing.sw.IsRunning)
            {
                timing.checkPointCounter++;
                var elapsed = timing.sw.ElapsedMilliseconds;
                Debug.Log($"{title ?? "Time"}: <color=#00FF00>{elapsed} ms</color>");
                timing.prevCheckpoint = elapsed;
            }
        }
    }
}