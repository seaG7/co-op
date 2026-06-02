using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroWorldNS
{
    [DefaultExecutionOrder(-20)]
    public class Dispatcher : MonoBehaviour
    {
        public static Dispatcher Instance { get; private set; }
        public static event Action BeforeUpdate;
        public static event Action BeforeFixedUpdate;
        public static event Action OnAwake;
        public static event Action OnStart;
        public static event Action OnUpdate;
        public static event Action Destroyed;
        public static event Action LateAwake;
        public static event Action LateLateAwake;
        public static event Action LateStart;
        public static event Action LateUpdate;
        public static event Action LateLateUpdate;
        public static event Action LateFixedUpdate;
        public static event Action LateLateFixedUpdate;

        public readonly static LinkedList<Action> OnUpdateFast = new LinkedList<Action>();
        public readonly static List<Action> OnUpdateOnePerFrame = new List<Action>();

        private readonly EffectiveLinkedList<DispatcherItem> list = new EffectiveLinkedList<DispatcherItem>();
        private readonly ConcurrentQueue<DispatcherItem> secondaryQueue = new ConcurrentQueue<DispatcherItem>();
        private readonly EffectiveLinkedList<DispatcherItem> listForFixedUpdate = new EffectiveLinkedList<DispatcherItem>();
        private static readonly EffectiveLinkedList<DispatcherItem> rareUpdateList = new EffectiveLinkedList<DispatcherItem>();
        private readonly object _lock = new object();
        private const string autoLoadPrefabName = "AutoLoad";

        public static new Coroutine StartCoroutine(IEnumerator routine) => (Instance as MonoBehaviour).StartCoroutine(routine);
        public static new void StopCoroutine(Coroutine routine) => (Instance as MonoBehaviour).StopCoroutine(routine);

        public static int FrameCountFixedUpdate { get; private set; }

        [RuntimeInitializeOnLoadMethod]
        static void AutoCreate()
        {
            var go = new GameObject("Dispatcher");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<Dispatcher>();
            go.AddComponent<LateDispatcher>();

            LateDispatcher.LateAwake += CallLateAwake;
            LateDispatcher.LateStart += CallLateStart;
            LateDispatcher.LateFixedUpdate += CallLateFixedUpdate;
            LateDispatcher.LateUpdate += CallLateUpdate;

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

            CallLateAwake();
        }

        private void OnDestroy()
        {
            try
            {
                Destroyed?.Invoke();
                rareUpdateList.Clear();
            }
            catch { }
        }

        private static void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            CallLateAwake();
        }

        public void Awake()
        {
            Instance = this;

            secondaryThread = new Thread(SecondaryLoop) { IsBackground = true, Name = "DispatcherSecondaryLoop" };
            secondaryThread.Start();

            //add global prefab
            var autoLoads = Resources.LoadAll<GameObject>(autoLoadPrefabName);
            foreach (var a in autoLoads)
                Instantiate(a, this.transform).name = autoLoadPrefabName;

            try
            {
                OnAwake?.Invoke();
            }
            catch { }
        }

        public void Start()
        {
            try
            {
                OnStart?.Invoke();
            }
            catch { }

            // start rare update loop
            StartCoroutine(ExecuteRareUpdate());
        }

        private static void CallLateAwake()
        {
            try
            {
                LateAwake?.Invoke();
            }
            catch { }

            try
            {
                LateLateAwake?.Invoke();
            }
            catch { }
        }

        private static void CallLateStart()
        {
            try
            {
                LateStart?.Invoke();
            }
            catch { }
        }

        private static void CallLateFixedUpdate()
        {
            try
            {
                LateFixedUpdate?.Invoke();
            }
            catch { }

            try
            {
                LateLateFixedUpdate?.Invoke();
            }
            catch { }
        }

        private static void CallLateUpdate()
        {
            try
            {
                LateUpdate?.Invoke();
            }
            catch { }

            try
            {
                LateLateUpdate?.Invoke();
            }
            catch { }
        }

        void FixedUpdate()
        {
            FrameCountFixedUpdate++;

            try
            {
                BeforeFixedUpdate?.Invoke();
            }
            catch { }

            ExecuteQueue(listForFixedUpdate);
        }

        static int OnUpdateOnePerFrameCounter = 0;

        void Update()
        {
            try
            {
                BeforeUpdate?.Invoke();
            }
            catch { }

            try
            {
                OnUpdate?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (OnUpdateOnePerFrame.Count > 0)
                try
                {
                    OnUpdateOnePerFrameCounter = OnUpdateOnePerFrameCounter % OnUpdateOnePerFrame.Count;
                    OnUpdateOnePerFrame[OnUpdateOnePerFrameCounter++].Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

            {
                var node = OnUpdateFast.First;
                while (node != null)
                {
                    try
                    {
                        node.Value.Invoke();
                    }
                    catch { }
                    node = node.Next;
                }
            }

            ExecuteQueue(list);
        }

        #region RareUpdate

        public static DispatcherItem EnqueueRareUpdate(Action action)
        {
            var item = new DispatcherItem() { Action = action };
            rareUpdateList.AddLast(item);

            return item;
        }

        private IEnumerator ExecuteRareUpdate()
        {
            const float MaxBuildDutyPerFrameInMs = 1000 / 120f;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                foreach (var node in rareUpdateList)
                {
                    try
                    {
                        node.Action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        list.Remove(node);
                        Debug.LogException(ex);
                        continue;
                    }
                    if (sw.ElapsedMilliseconds < MaxBuildDutyPerFrameInMs)
                        continue;

                    sw.Reset();
                    yield return null;
                }
                yield return null;
            }
        }

        #endregion

        private void ExecuteQueue(EffectiveLinkedList<DispatcherItem> list)
        {
            lock (_lock)
            {
                var time = Time.time;
                foreach (var node in list)
                {
                    bool isConditionTrue = false;

                    try
                    {
                        isConditionTrue = (time > node.Time) && (node.ExecuteCondition == null || node.ExecuteCondition());
                    }
                    catch (Exception ex)
                    {
                        list.Remove(node);
                        Debug.LogException(ex);
                        continue;
                    }

                    if (isConditionTrue)
                        try
                        {
                            node.Action?.Invoke();
                            list.Remove(node);
                        }
                        catch (Exception ex)
                        {
                            list.Remove(node);
                            Debug.LogException(ex);
                        }
                }
            }
        }

        /// <summary> Will be executed in next frame </summary>
        public static DispatcherItem Enqueue(Action action)
        {
            return Enqueue(null, action);
        }

        public static DispatcherItem Enqueue(Action action, float delay)
        {
            var item = new DispatcherItem() { Action = action, Time = Time.time + delay };
            lock (Instance._lock)
            {
                Instance.list.AddLast(item);
            }

            return item;
        }

        public static DispatcherItem Enqueue(Func<bool> executeCondition, Action action)
        {
            var item = new DispatcherItem() { Action = action, ExecuteCondition = executeCondition };
            lock (Instance._lock)
            {
                Instance.list.AddLast(item);
            }

            return item;
        }

        /// <summary> Will be executed in next frame </summary>
        public static DispatcherItem EnqueueInFixedUpdate(Action action)
        {
            return EnqueueInFixedUpdate(null, action);
        }

        public static DispatcherItem EnqueueInFixedUpdate(Action action, float delay)
        {
            var item = new DispatcherItem() { Action = action, Time = Time.time + delay };
            lock (Instance._lock)
            {
                Instance.listForFixedUpdate.AddLast(item);
            }

            return item;
        }

        public static DispatcherItem EnqueueInFixedUpdate(Func<bool> executeCondition, Action action)
        {
            var item = new DispatcherItem() { Action = action, ExecuteCondition = executeCondition };
            lock (Instance._lock)
            {
                Instance.listForFixedUpdate.AddLast(item);
            }

            return item;
        }

        public class DispatcherItem : ILinkedListNode
        {
            public Func<bool> ExecuteCondition;
            public Action Action;
            public float Time;

            public bool IsRemoved { get; set; }
            public ILinkedListNode Next { get; set; }

            public void Cancel()
            {
                IsRemoved = true;
            }
        }

        #region Secondary Thread

        static Thread secondaryThread;

        public static DispatcherItem EnqueueInSecondaryThread(Action action)
        {
            var item = new DispatcherItem() { Action = action };
            Instance.secondaryQueue.Enqueue(item);
            allowSecondaryLoop = true;

            return item;
        }

        public static IEnumerator ExecuteInSecondaryThread(Action action)
        {
            var item = EnqueueInSecondaryThread(action);

            while (!item.IsRemoved)
                yield return null;
        }

        static bool allowSecondaryLoop;
        static bool secondaryLoopAbort;

        private static void SecondaryLoop()
        {
            while (true)
            {
                Thread.Sleep(0);

                if (!allowSecondaryLoop)//limit thread activity
                    continue;

                while (Instance.secondaryQueue.TryDequeue(out var item))
                    try
                    {
                        if (secondaryLoopAbort)
                        {
                            secondaryLoopAbort = false;
                            return;
                        }

                        if (!item.IsRemoved)
                        {
                            item?.Action?.Invoke();
                            item.IsRemoved = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                        item.IsRemoved = true;
                    }

                if (secondaryLoopAbort)
                {
                    secondaryLoopAbort = false;
                    return;
                }

                allowSecondaryLoop = false;
            }
        }
        #endregion
    }

    [DefaultExecutionOrder(100)]
    public class LateDispatcher : MonoBehaviour
    {
        public static event Action LateAwake;
        public static event Action LateStart;
        public static event Action LateUpdate;
        public static event Action LateFixedUpdate;

        private void Update()
        {
            LateUpdate?.Invoke();
        }

        private void Awake()
        {
            LateAwake?.Invoke();
        }

        private void Start()
        {
            LateStart?.Invoke();
        }

        private void FixedUpdate()
        {
            LateFixedUpdate?.Invoke();
        }
    }
}