﻿using System;
using System.Collections;
using TimeStretch.Cache;
using UnityEngine;

namespace TimeStretch.Utils
{
    public class CoroutineRunner : MonoBehaviour
    {
        public static event Action OnUpdate;
        private static CoroutineRunner _instance;

        public static void Run(IEnumerator routine)
        {
            if (_instance == null)
            {
                var go = new GameObject("TimeStretchCoroutineRunner");
                GameObject.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineRunner>();
            }

            _instance.StartCoroutine(routine);
        }
        public static void RegisterUpdate(Action action)
        {
            if (_instance == null)
            {
                var go = new GameObject("TimeStretchCoroutineRunner");
                GameObject.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineRunner>();
            }

            OnUpdate += action;
        }

        public static void UnregisterUpdate(Action action)
        {
            if (_instance != null)
            {
                OnUpdate -= action;
            }
        }

        private void Update()
        {
            OnUpdate?.Invoke();
        }
    }
    
   
    public class TimeStretchManager : MonoBehaviour
    {
        private static TimeStretchManager _instance;

        public static void Initialize()
        {
            if (_instance != null) return;

            var go = new GameObject("TimeStretchManager (DontDestroy)");
            _instance = go.AddComponent<TimeStretchManager>();
            DontDestroyOnLoad(go);

            CacheObject.StartTrackingCoroutine(_instance);
        }
      
        public static void StartRoutine(IEnumerator routine)
        {
            if (_instance == null)
            {
                BatchLogger.Warn("⚠️ TimeStretchManager non initialisé, impossible de lancer la coroutine.");
                return;
            }

            _instance.StartCoroutine(routine);
        }
    }
    
    
}