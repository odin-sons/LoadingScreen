namespace LoadingScreen;

using System;
using System.Collections;
using System.Collections.Generic;

using global::LoadingScreen.Model;

using SoftReferenceableAssets.SceneManagement;

using UnityEngine;
using UnityEngine.EventSystems;

using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using UnityScene = UnityEngine.SceneManagement.Scene;
using UnityLoadSceneMode = UnityEngine.SceneManagement.LoadSceneMode;

static class WorldSceneLoad {
  static readonly List<BaseInputModule> DisabledInputModules = [];
  static SceneLoadTiming _timing;
  static ILoadSceneAsyncOperation _operation;
  static bool _budgetRequested;

  internal static MonoBehaviour Host { get; set; }

  internal static bool InProgress => _operation != null;

  static double Now => Time.realtimeSinceStartupAsDouble;

  internal static bool TryLoadInBackground(FejdStartup startup) {
    if (InProgress) return true;
    BeginTiming("sync");
    if (!PluginConfig.IsModEnabled.Value || !PluginConfig.LoadWorldInBackground.Value || !Host) return false;

    try {
      startup.m_loading.SetActive(true);
      ZCursor.Hide();
      RequestBudget();
      BlockMenuInput();
      _timing = new SceneLoadTiming("async", Now);
      _operation = SystemResourceManager.FastLoadSceneAsync(startup.m_mainScene);
    } catch (Exception exception) {
      _operation = null;
      RestoreMenuInput();
      ReleaseBudget();
      _timing = new SceneLoadTiming("sync", Now);
      Log.Plugin.Warning(LogContext.Message("Could not load the world scene in the background; loading it synchronously.",
          ("setting", "Load world in background"), ("error", exception.Message)));
      Log.Plugin.Debug(exception);
      return false;
    }

    ILoadSceneAsyncOperation operation = _operation;
    operation.Completed += _ => Finish(operation);
    if (operation.IsDone) Finish(operation);
    startup.m_startingWorld = false;
    Log.Plugin.Debug(LogContext.Message("Started loading the world scene in the background.",
        ("backgroundLoadingPriority", Application.backgroundLoadingPriority)));
    Host.StartCoroutine(WatchProgress(operation));
    return true;
  }

  static void BeginTiming(string mode) {
    _timing = new SceneLoadTiming(mode, Now);
    UnitySceneManager.sceneLoaded -= OnSceneLoaded;
    UnitySceneManager.sceneLoaded += OnSceneLoaded;
  }

  static IEnumerator WatchProgress(ILoadSceneAsyncOperation operation) {
    while (_operation == operation && !operation.IsDone) {
      _timing?.ObserveProgress(Now, operation.Progress);
      yield return null;
    }
  }

  static void OnSceneLoaded(UnityScene scene, UnityLoadSceneMode mode) {
    if (mode != UnityLoadSceneMode.Single) return;
    UnitySceneManager.sceneLoaded -= OnSceneLoaded;
    if (_operation != null) Finish(_operation);
    if (_timing == null || _timing.IsDone) return;
    _timing.MarkDone(Now);
    Log.Plugin.Info(LogContext.Message("Loaded the world scene.",
        [.. _timing.Details(), ("scene", scene.name)]));
  }

  static void Finish(ILoadSceneAsyncOperation operation) {
    if (_operation != operation) return;
    _operation = null;
    RestoreMenuInput();
    ReleaseBudget();
  }

  static void RequestBudget() {
    BackgroundLoadingBudgetController.RequestLoadingBudget(ThreadPriority.High);
    _budgetRequested = true;
  }

  static void ReleaseBudget() {
    if (!_budgetRequested) return;
    _budgetRequested = false;
    BackgroundLoadingBudgetController.ReleaseLoadingBudgetRequest(ThreadPriority.High);
  }

  static void BlockMenuInput() {
    EventSystem eventSystem = EventSystem.current;
    if (!eventSystem) return;
    eventSystem.SetSelectedGameObject(null);
    foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>()) {
      if (!module.enabled) continue;
      module.enabled = false;
      DisabledInputModules.Add(module);
    }
  }

  static void RestoreMenuInput() {
    foreach (BaseInputModule module in DisabledInputModules) {
      if (module) module.enabled = true;
    }
    DisabledInputModules.Clear();
  }
}
