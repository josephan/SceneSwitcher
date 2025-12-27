using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SceneSwitcher.Editor
{
    [InitializeOnLoad]
    internal abstract partial class SceneSwitcherWindow : EditorWindow
    {
        public static readonly System.Version Version = new System.Version("2.0.0");

        public const int SeparatorMenuItemPriority = 154;

        private Vector2 scrollPosition;
        private bool? closeDialogWhenSceneLoads;
        private bool wasCompleteSceneSearchPerformed;
        private string displayMessage;
        private System.Action delayedAction;
        private GUIStyle sceneButtonStyle;
        private GUIStyle highlightButtonStyle;
        private GUIStyle indexLabelStyle;
        private int indexLabelMaxLength;
        private string[] previousCachedScenePaths;
        private List<string> cachedScenePaths;
        private string[] cachedSceneTexts;
        private double cachedSceneTime;
        private static double refreshedSceneTime;
        private static string sceneRestoreOnPlayStop;

        private const string SceneFileExtension = ".unity";
        private const string ScenePathsPreference = "SceneSwitcher.RecentlyUsedScenes";
        private const string CloseDialogPreference = "SceneSwitcher.CloseDialogWhenSceneLoads";
        private const string SceneRestorePreference = "SceneSwitcher.SceneRestore";
        private const string HighlightText = "\u2022";
        private const string CloseDialogText = "Close dialog after load";
        private const string SearchSceneText = "Search for more...";

        private static readonly char PreferenceDataSeparator = Path.PathSeparator;
        private static readonly System.IFormatProvider InvariantCultureFormat = System.Globalization.CultureInfo.InvariantCulture;
        private static readonly System.StringComparer ProjectPathComparer = System.StringComparer.Ordinal;

        public static string CurrentAssets => Path.GetFullPath(Application.dataPath);
        public static string CurrentProject => Path.GetFullPath(Path.Combine(CurrentAssets, ".."));
        public static string CurrentScene { get; protected set; }

        protected virtual bool CloseDialogWhenSceneLoads
        {
            get
            {
                if (!closeDialogWhenSceneLoads.HasValue)
                {
                    closeDialogWhenSceneLoads = EditorPrefs.GetBool(CloseDialogPreference, true);
                }
                return closeDialogWhenSceneLoads.Value;
            }
            set
            {
                if (!closeDialogWhenSceneLoads.HasValue || closeDialogWhenSceneLoads.Value != value)
                {
                    closeDialogWhenSceneLoads = value;
                    EditorPrefs.SetBool(CloseDialogPreference, value);
                }
            }
        }

        protected static string SceneRestoreOnPlayStop
        {
            get
            {
                if (sceneRestoreOnPlayStop == null)
                {
                    sceneRestoreOnPlayStop = GetSceneRestoreData() ?? string.Empty;
                }
                return sceneRestoreOnPlayStop;
            }
            set
            {
                if (sceneRestoreOnPlayStop == null || sceneRestoreOnPlayStop != (value ?? string.Empty))
                {
                    sceneRestoreOnPlayStop = value ?? string.Empty;
                    SetSceneRestoreData(sceneRestoreOnPlayStop);
                }
            }
        }

        static SceneSwitcherWindow()
        {
            EditorApplication.hierarchyChanged += OnHierarchyWindowChanged;
            EditorApplication.playModeStateChanged += OnPlaymodeStateChanged;
        }

        private static void OnHierarchyWindowChanged()
        {
            DetectSceneChange();
        }

        protected static void DetectSceneChange()
        {
            if ((CurrentScene ?? "") != (GetCurrentScenePath() ?? ""))
            {
                OnSceneChanged();
            }
        }

        private static void OnPlaymodeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(SceneRestoreOnPlayStop))
            {
                string scenePath = SceneRestoreOnPlayStop;
                SceneRestoreOnPlayStop = null;
                OpenScene(scenePath, false);
            }
        }

        protected static void OnSceneChanged()
        {
            refreshedSceneTime = EditorApplication.timeSinceStartup;
            CurrentScene = GetCurrentScenePath();
            string currentProject = CurrentProject;
            string currentScene = CurrentScene;

            if (!string.IsNullOrEmpty(currentProject))
            {
                Dictionary<string, List<string>> projectScenes = LoadProjectScenePaths(true);
                List<string> scenePaths = projectScenes[currentProject];

                bool isListChanged = false;
                if (!string.IsNullOrEmpty(currentScene))
                {
                    int currentSceneIndex = scenePaths.IndexOf(currentScene);
                    if (currentSceneIndex != 0)
                    {
                        isListChanged = true;
                        if (currentSceneIndex > 0)
                        {
                            scenePaths.RemoveAt(currentSceneIndex);
                        }
                        scenePaths.Insert(0, currentScene);
                    }
                }

                for (int sceneIndex = scenePaths.Count - 1; sceneIndex >= 0; sceneIndex--)
                {
                    string scene = scenePaths[sceneIndex];
                    if (!string.IsNullOrEmpty(scene))
                    {
                        if (File.Exists(Path.Combine(currentProject, scene)) || scene == currentScene)
                        {
                            continue;
                        }
                    }
                    isListChanged = true;
                    scenePaths.RemoveAt(sceneIndex);
                }

                if (isListChanged)
                {
                    projectScenes[currentProject] = scenePaths;
                    SaveProjectScenePaths(projectScenes);
                }
            }
        }

        protected static Dictionary<string, List<string>> LoadProjectScenePaths(bool includeEntryForCurrentProject = false)
        {
            var projectScenes = new Dictionary<string, List<string>>(ProjectPathComparer);
            string savedData = EditorPrefs.GetString(ScenePathsPreference);
            string[] data = !string.IsNullOrEmpty(savedData) ? savedData.Split(PreferenceDataSeparator) : null;
            int dataCount = data?.Length ?? 0;
            int dataIndex = 0;

            while (dataIndex < dataCount)
            {
                string projectPath = data[dataIndex++];
                if (dataIndex >= dataCount)
                {
                    Debug.LogWarning($"Incomplete {ScenePathsPreference} project data!");
                    break;
                }

                if (!int.TryParse(data[dataIndex++], System.Globalization.NumberStyles.Integer, InvariantCultureFormat, out int sceneCount))
                {
                    Debug.LogWarning($"Invalid {ScenePathsPreference} count data! ({data[dataIndex - 1]})");
                    break;
                }

                var scenePaths = new List<string>();
                if (sceneCount <= 0)
                {
                    if (dataIndex >= dataCount)
                    {
                        Debug.LogWarning($"Invalid {ScenePathsPreference} scene data!");
                        break;
                    }

                    if (!string.IsNullOrEmpty(data[dataIndex++]))
                    {
                        Debug.LogWarning($"Bad {ScenePathsPreference} scene data! ({data[dataIndex - 1]})");
                        break;
                    }
                }
                else
                {
                    for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
                    {
                        if (dataIndex >= dataCount)
                        {
                            Debug.LogWarning($"Incomplete {ScenePathsPreference} scene data!");
                            break;
                        }
                        scenePaths.Add(data[dataIndex++]);
                    }
                }

                projectScenes[projectPath] = scenePaths;
            }

            if (includeEntryForCurrentProject)
            {
                string currentProject = CurrentProject;
                if (!projectScenes.TryGetValue(currentProject, out List<string> scenePaths) || scenePaths == null)
                {
                    scenePaths = new List<string>();
                    projectScenes[currentProject] = scenePaths;
                }
            }

            return projectScenes;
        }

        protected static void SaveProjectScenePaths(Dictionary<string, List<string>> projectScenes, bool discardOldProjects = true)
        {
            if (discardOldProjects)
            {
                var recentProjectPaths = new HashSet<string>(
                    Enumerable.Range(0, 20)
                    .Select(i => EditorPrefs.GetString("RecentlyUsedProjectPaths-" + i))
                    .Where(projectPath => !string.IsNullOrEmpty(projectPath))
                    .Select(projectPath => Path.GetFullPath(projectPath)));

                string currentProject = CurrentProject;
                foreach (string projectPath in projectScenes.Keys.ToArray())
                {
                    if (!recentProjectPaths.Contains(projectPath) && currentProject != projectPath)
                    {
                        projectScenes.Remove(projectPath);
                    }
                }
            }

            string separator = PreferenceDataSeparator.ToString(InvariantCultureFormat);
            string[] projectData = projectScenes
                .Select(dictionaryItem => string.Format(InvariantCultureFormat, "{1}{0}{2}{0}{3}", separator, dictionaryItem.Key, dictionaryItem.Value.Count, string.Join(separator, dictionaryItem.Value.ToArray())))
                .ToArray();
            string data = string.Join(separator, projectData);
            EditorPrefs.SetString(ScenePathsPreference, data);
        }

        protected static string GetSceneRestoreData()
        {
            var sceneRestoreData = LoadSceneRestoreData(true)[CurrentProject];
            if (sceneRestoreData.Key < GetSceneRestoreTimestampAtStartup())
            {
                return string.Empty;
            }
            return sceneRestoreData.Value ?? string.Empty;
        }

        protected static void SetSceneRestoreData(string scenePath)
        {
            var sceneRestoreData = LoadSceneRestoreData(false);
            sceneRestoreData[CurrentProject] = new KeyValuePair<long, string>(GetSceneRestoreTimestampAtPresent(), scenePath ?? string.Empty);
            SaveSceneRestoreData(sceneRestoreData);
        }

        private static Dictionary<string, KeyValuePair<long, string>> LoadSceneRestoreData(bool includeEntryForCurrentProject = false)
        {
            var sceneRestoreData = new Dictionary<string, KeyValuePair<long, string>>(ProjectPathComparer);
            string savedData = EditorPrefs.GetString(SceneRestorePreference);
            string[] data = !string.IsNullOrEmpty(savedData) ? savedData.Split(PreferenceDataSeparator) : null;
            int dataCount = data?.Length ?? 0;
            int dataIndex = 0;

            while (dataIndex < dataCount)
            {
                if (!long.TryParse(data[dataIndex++], System.Globalization.NumberStyles.Integer, InvariantCultureFormat, out long timestamp))
                {
                    Debug.LogWarning($"Invalid {SceneRestorePreference} timestamp data! ({data[dataIndex - 1]})");
                    break;
                }

                if (dataIndex > dataCount - 2)
                {
                    Debug.LogWarning($"Incomplete {SceneRestorePreference} project data!");
                    break;
                }

                string projectPath = data[dataIndex++];
                string scenePath = data[dataIndex++];
                sceneRestoreData[projectPath] = new KeyValuePair<long, string>(timestamp, scenePath);
            }

            if (includeEntryForCurrentProject)
            {
                string currentProject = CurrentProject;
                if (!sceneRestoreData.TryGetValue(currentProject, out KeyValuePair<long, string> sceneData))
                {
                    sceneData = new KeyValuePair<long, string>(GetSceneRestoreTimestampAtPresent(), string.Empty);
                    sceneRestoreData[currentProject] = sceneData;
                }
            }

            return sceneRestoreData;
        }

        private static void SaveSceneRestoreData(Dictionary<string, KeyValuePair<long, string>> sceneRestoreData, bool discardOldData = true)
        {
            if (discardOldData)
            {
                const int MaxItemCount = 10;
                const long ExpirationSeconds = 7 * 24 * 60 * 60;
                long timeNow = GetSceneRestoreTimestampAtPresent();

                var expiredDataKeys = sceneRestoreData
                    .Where(item => System.Math.Abs(timeNow - item.Value.Key) > ExpirationSeconds)
                    .Select(item => item.Key)
                    .ToList();

                foreach (var expiredDataKey in expiredDataKeys)
                {
                    sceneRestoreData.Remove(expiredDataKey);
                }

                if (sceneRestoreData.Count > MaxItemCount)
                {
                    expiredDataKeys.Clear();
                    expiredDataKeys.AddRange(sceneRestoreData
                        .OrderBy(item => item.Value.Key)
                        .Take(sceneRestoreData.Count - MaxItemCount)
                        .Select(item => item.Key));

                    foreach (var expiredDataKey in expiredDataKeys)
                    {
                        sceneRestoreData.Remove(expiredDataKey);
                    }
                }
            }

            string separator = PreferenceDataSeparator.ToString(InvariantCultureFormat);
            string[] restoreData = sceneRestoreData
                .Select(item => string.Format(InvariantCultureFormat, "{1}{0}{2}{0}{3}", separator, item.Value.Key, item.Key, item.Value.Value))
                .ToArray();
            string data = string.Join(separator, restoreData);
            EditorPrefs.SetString(SceneRestorePreference, data);
        }

        protected static long GetSceneRestoreTimestampAtPresent()
        {
            return GetSceneRestoreTimestamp(System.DateTime.UtcNow);
        }

        protected static long GetSceneRestoreTimestampAtStartup()
        {
            System.DateTime timestampUtc = System.DateTime.UtcNow;
            return GetSceneRestoreTimestamp(timestampUtc.AddSeconds(-EditorApplication.timeSinceStartup));
        }

        protected static long GetSceneRestoreTimestamp(System.DateTime timestampUtc)
        {
            return (timestampUtc.Ticks - 635241096000000000L) / 10000000L;
        }

        protected virtual GUIStyle GetIndexLabelStyle(int indexMaximum)
        {
            if (indexLabelStyle == null)
            {
                indexLabelStyle = new GUIStyle((GUIStyle)"label")
                {
                    alignment = TextAnchor.MiddleRight
                };
                indexLabelStyle.padding.top += 2;
            }

            if (indexLabelMaxLength != indexMaximum || indexLabelMaxLength <= 0)
            {
                GUIStyle sceneButtonStyle = GetSceneButtonStyle();
                indexLabelMaxLength = indexMaximum;
                int digits = indexMaximum < 10 ? 1 : 1 + Mathf.FloorToInt(Mathf.Log10(indexMaximum));
                indexLabelStyle.fixedWidth = sceneButtonStyle.CalcSize(new GUIContent(new string('X', digits))).x;
            }

            return indexLabelStyle;
        }

        protected virtual GUIStyle GetHighlightButtonStyle()
        {
            if (highlightButtonStyle == null)
            {
                highlightButtonStyle = new GUIStyle((GUIStyle)"button")
                {
                    fixedWidth = new GUIStyle((GUIStyle)"button").CalcSize(new GUIContent(HighlightText)).x
                };
            }
            return highlightButtonStyle;
        }

        protected virtual GUIStyle GetSceneButtonStyle()
        {
            if (sceneButtonStyle == null)
            {
                sceneButtonStyle = new GUIStyle((GUIStyle)"button")
                {
                    alignment = TextAnchor.MiddleLeft
                };
            }
            return sceneButtonStyle;
        }

        protected virtual void OnGUI()
        {
            DetectSceneChange();

            int selectedButtonIndex = -1;
            bool isHelpRequested = false;
            bool isCompleteSceneSearchRequested = false;
            var currentEvent = Event.current;

            if (currentEvent.type == EventType.KeyDown)
            {
                if (currentEvent.keyCode >= KeyCode.Alpha0 && currentEvent.keyCode <= KeyCode.Alpha9)
                {
                    selectedButtonIndex = currentEvent.keyCode - KeyCode.Alpha0;
                }
                else if (currentEvent.keyCode >= KeyCode.Keypad0 && currentEvent.keyCode <= KeyCode.Keypad9)
                {
                    selectedButtonIndex = currentEvent.keyCode - KeyCode.Keypad0;
                }

                if (selectedButtonIndex >= 0)
                {
                    EventModifiers eventModifiers = GetCurrentEventModifiers();
                    if ((eventModifiers & EventModifiers.Shift) != 0)
                    {
                        selectedButtonIndex += 10;
                    }
                }
                else
                {
                    switch (currentEvent.keyCode)
                    {
                        case KeyCode.Space:
                            CloseDialogWhenSceneLoads = !CloseDialogWhenSceneLoads;
                            Repaint();
                            break;
                        case KeyCode.F1:
                            isHelpRequested = true;
                            break;
                        case KeyCode.Return:
                        case KeyCode.KeypadEnter:
                            isCompleteSceneSearchRequested = true;
                            break;
                    }
                }
            }

            string currentProject = CurrentProject;
            var scenePaths = cachedScenePaths;

            if (scenePaths == null || cachedSceneTime < refreshedSceneTime)
            {
                cachedSceneTime = refreshedSceneTime;
                scenePaths = cachedScenePaths = LoadProjectScenePaths(true)[currentProject];
                wasCompleteSceneSearchPerformed = false;
                AddExtraScenePaths(false);
                RefreshScenePathTexts();
            }

            string displayMessage = this.displayMessage;
            if (!string.IsNullOrEmpty(displayMessage))
            {
                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox(displayMessage, MessageType.None);
                GUILayout.Space(2);
                GUILayout.EndVertical();
                return;
            }

            var sceneTexts = cachedSceneTexts;
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginVertical();

            GUIStyle highlightButtonStyle = GetHighlightButtonStyle();
            int scenePathsMaxIndex = scenePaths.Count - 1;

            if (scenePathsMaxIndex < 0)
            {
                EditorGUILayout.HelpBox("0 scenes found.", MessageType.None);
            }
            else
            {
                GUIStyle sceneButtonStyle = GetSceneButtonStyle();
                GUIStyle indexLabelStyle = GetIndexLabelStyle(scenePathsMaxIndex);

                GUILayout.BeginHorizontal();
                GUILayout.Space(2);
                GUILayout.BeginVertical();

                for (int sceneIndex = 0; sceneIndex <= scenePathsMaxIndex; sceneIndex++)
                {
                    bool isActionRequested;
                    string scenePath = scenePaths[sceneIndex];

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{sceneIndex}.", indexLabelStyle);
                    isActionRequested = GUILayout.Button(sceneTexts[sceneIndex], sceneButtonStyle);

                    if (GUILayout.Button(new GUIContent(HighlightText, scenePath), highlightButtonStyle))
                    {
                        EventModifiers eventModifiers = GetCurrentEventModifiers();
                        HighlightOrSelectScene(scenePath, eventModifiers);
                    }
                    GUILayout.EndHorizontal();

                    if (!isActionRequested && selectedButtonIndex == sceneIndex)
                    {
                        isActionRequested = true;
                    }

                    if (isActionRequested && (EditorApplication.isPlaying || SaveCurrentModifiedScenesIfUserWantsTo()))
                    {
                        EventModifiers eventModifiers = GetCurrentEventModifiers();
                        ScheduleDelayedAction(() => LoadScene(scenePath, eventModifiers), $"Loading scene...\n{scenePath}");
                        Repaint();
                    }
                }

                if (!wasCompleteSceneSearchPerformed)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", indexLabelStyle);
                    if (GUILayout.Button(new GUIContent(SearchSceneText, "Find all scenes in project (can be slow)"), sceneButtonStyle) || isCompleteSceneSearchRequested)
                    {
                        AddExtraScenePaths(true);
                        RefreshScenePathTexts();
                        Repaint();
                    }
                    GUILayout.Space(highlightButtonStyle.fixedWidth + sceneButtonStyle.margin.right);
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            CloseDialogWhenSceneLoads = GUILayout.Toggle(CloseDialogWhenSceneLoads, CloseDialogText);
            GUILayout.FlexibleSpace();

            var helpContent = new GUIContent("?", "Help (F1)");
            isHelpRequested = GUILayout.Button(helpContent, EditorStyles.miniButton) || isHelpRequested;

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                GUILayout.Space(highlightButtonStyle.fixedWidth * 0.65f);
            }

            if (isHelpRequested)
            {
                DisplayHelpInfo();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        protected virtual void AddExtraScenePaths(bool isFullSearchRequested)
        {
            List<string> scenePaths = cachedScenePaths;
            string currentProject = CurrentProject;
            int currentProjectLength = currentProject.Length;

            void AddScenePathIfMissing(string scenePath)
            {
                if (!string.IsNullOrEmpty(scenePath))
                {
                    if (!Path.IsPathRooted(scenePath))
                    {
                        scenePath = Path.Combine(currentProject, scenePath);
                    }

                    scenePath = Path.GetFullPath(scenePath);
                    if (scenePath.Length > currentProjectLength && File.Exists(Path.Combine(currentProject, scenePath)))
                    {
                        scenePath = scenePath.Substring(currentProjectLength).Replace('\\', '/').TrimStart('/');
                        if (!string.IsNullOrEmpty(scenePath) && !scenePaths.Contains(scenePath))
                        {
                            scenePaths.Add(scenePath);
                        }
                    }
                }
            }

            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes != null && buildScenes.Length > 0)
            {
                foreach (var scene in buildScenes)
                {
                    if (scene != null && scene.enabled)
                    {
                        AddScenePathIfMissing(scene.path);
                    }
                }

                foreach (var scene in buildScenes)
                {
                    if (scene != null && !scene.enabled)
                    {
                        AddScenePathIfMissing(scene.path);
                    }
                }
            }

            var previousCachedScenePaths = this.previousCachedScenePaths;
            if (previousCachedScenePaths != null && previousCachedScenePaths.Length > 0)
            {
                foreach (string scenePath in previousCachedScenePaths)
                {
                    AddScenePathIfMissing(scenePath);
                }
            }

            if (isFullSearchRequested || scenePaths.Count <= 0)
            {
                wasCompleteSceneSearchPerformed = true;
                string[] allSceneFiles = Directory.GetFiles(CurrentAssets, "*" + SceneFileExtension, SearchOption.AllDirectories);
                if (allSceneFiles != null && allSceneFiles.Length > 0)
                {
                    foreach (string scenePath in allSceneFiles)
                    {
                        AddScenePathIfMissing(scenePath);
                    }
                }
            }

            this.previousCachedScenePaths = scenePaths.ToArray();
        }

        protected virtual void Update()
        {
            var delayedAction = this.delayedAction;
            if (delayedAction != null)
            {
                this.delayedAction = null;
                delayedAction();
            }
        }

        protected virtual void ScheduleDelayedAction(System.Action delayedAction, string displayMessage = null)
        {
            if (string.IsNullOrEmpty(displayMessage))
            {
                this.delayedAction = delayedAction;
            }
            else
            {
                this.delayedAction = () =>
                {
                    this.displayMessage = displayMessage;
                    Repaint();

                    this.delayedAction = () =>
                    {
                        if (string.CompareOrdinal(this.displayMessage, displayMessage) == 0)
                        {
                            this.displayMessage = null;
                        }

                        delayedAction?.Invoke();
                    };
                };
            }
        }

        protected virtual void LoadScene(string scenePath, EventModifiers eventModifiers)
        {
            if (!string.IsNullOrEmpty(scenePath))
            {
                eventModifiers &= EventModifiers.Alt | EventModifiers.Control | EventModifiers.Command;
                bool isModeAdditive = (eventModifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
                bool isPlayRequest = (eventModifiers & EventModifiers.Alt) != 0;

                if (isPlayRequest)
                {
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = false;
                    }

                    if (scenePath != GetCurrentScenePath() && string.IsNullOrEmpty(SceneRestoreOnPlayStop))
                    {
                        SceneRestoreOnPlayStop = GetCurrentScenePath();
                    }
                }

                cachedScenePaths = null;
                OpenScene(scenePath, isModeAdditive);

                if (isPlayRequest)
                {
                    EditorApplication.isPlaying = true;
                }

                if (CloseDialogWhenSceneLoads)
                {
                    Close();
                }
                else
                {
                    Repaint();
                }

                RepaintAllViews();
            }
        }

        protected virtual void HighlightOrSelectScene(string scenePath, EventModifiers eventModifiers)
        {
            var sceneAsset = AssetDatabase.LoadMainAssetAtPath(scenePath);
            if (sceneAsset)
            {
                Object[] selection = null;
                eventModifiers &= EventModifiers.Alt | EventModifiers.Control | EventModifiers.Command;

                switch (eventModifiers)
                {
                    case 0:
                        EditorGUIUtility.PingObject(sceneAsset);
                        break;

                    case EventModifiers.Alt:
                        selection = new Object[] { sceneAsset };
                        break;

                    case EventModifiers.Control:
                    case EventModifiers.Command:
                        selection = Selection.objects;
                        if (selection == null || selection.Length <= 0)
                        {
                            selection = new Object[] { sceneAsset };
                        }
                        else
                        {
                            var selectionList = new List<Object>(selection);
                            int i = selectionList.IndexOf(sceneAsset);
                            if (i < 0)
                            {
                                selectionList.Add(sceneAsset);
                            }
                            else
                            {
                                selectionList.RemoveAt(i);
                            }
                            selection = selectionList.ToArray();
                        }
                        break;
                }

                if (selection != null)
                {
                    Selection.objects = selection;
                }
            }
        }

        protected virtual void DisplayHelpInfo()
        {
            string title = $"About Scene Switcher v.{Version}";
            const string Bullet = "\u2022\u00A0";
            string message =
                Bullet + "Click on the named scene buttons to open the various scenes, which are ordered by most recent use.\n" +
                Bullet + "Use keys 0 through 9 as shortcuts for scenes 0 to 9, respectively, and Shift+0 through Shift+9 for scenes 10 through 19.\n" +
                Bullet + "Ctrl-click the scene buttons to additively load a specific scene into the current scene.\n" +
                Bullet + "Alt-click the scene buttons to play that scene but restore to the current scene when playing stops; Ctrl-Alt-click does an additive load followed by playing.\n" +
                Bullet + "Click on the small dotted buttons to highlight the scene in the Project window. Alt-click those buttons to select the scene asset, or Ctrl-click them for multi-selection of scene assets. Hover over them for a tooltip with the path to the scene asset in edit mode.\n" +
                Bullet + "Use F1 to open this help dialog, spacebar to toggle \"" + CloseDialogText.ToLower() + "\", or Enter to \"" + SearchSceneText.ToLower().TrimEnd('.') + "\".";
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        protected virtual EventModifiers GetCurrentEventModifiers()
        {
            Event currentEvent = Event.current;
            return currentEvent != null ? currentEvent.modifiers : 0;
        }

        protected virtual void RefreshScenePathTexts()
        {
            List<string> scenePaths = cachedScenePaths;
            string[] sceneTexts = cachedSceneTexts;

            if (scenePaths == null)
            {
                sceneTexts = null;
            }
            else
            {
                int sceneCount = scenePaths.Count;
                if (sceneTexts == null || sceneTexts.Length != sceneCount)
                {
                    sceneTexts = new string[sceneCount];
                }

                if (sceneCount > 0)
                {
                    if (scenePaths.Any(path => string.IsNullOrEmpty(path)))
                    {
                        sceneTexts = scenePaths.ToArray();
                    }
                    else if (sceneCount == 1)
                    {
                        sceneTexts[0] = Path.GetFileNameWithoutExtension(scenePaths[0]);
                    }
                    else
                    {
                        for (int i = 0; i < sceneCount; i++)
                        {
                            sceneTexts[i] = scenePaths[i];
                        }

                        const char PathSeparator = '/';
                        while (true)
                        {
                            int trimLength = sceneTexts[0].IndexOf(PathSeparator);
                            if (trimLength <= 0)
                            {
                                break;
                            }

                            string trimText = sceneTexts[0].Substring(0, trimLength);
                            if (sceneTexts.Any(text => trimLength != text.IndexOf(PathSeparator)) ||
                                sceneTexts.Any(text => !text.StartsWith(trimText)))
                            {
                                break;
                            }

                            trimLength++;
                            for (int i = 0; i < sceneCount; i++)
                            {
                                sceneTexts[i] = sceneTexts[i].Substring(trimLength);
                            }
                        }

                        if (sceneTexts.All(IsSceneFile))
                        {
                            for (int i = 0; i < sceneCount; i++)
                            {
                                sceneTexts[i] = $"{Path.GetDirectoryName(sceneTexts[i])}{PathSeparator}{Path.GetFileNameWithoutExtension(sceneTexts[i])}";
                                sceneTexts[i] = sceneTexts[i].TrimStart(PathSeparator);
                            }
                        }
                    }
                }
            }

            cachedSceneTexts = sceneTexts;
        }

        protected static bool IsSceneFile(string assetPath)
        {
            return assetPath != null && assetPath.EndsWith(SceneFileExtension);
        }

        protected partial class SceneAssetChangeDetector : AssetPostprocessor
        {
            protected static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                bool isSceneInvolved = false;

                if (movedAssets != null && movedAssets.Length > 0 && movedFromAssetPaths != null && movedFromAssetPaths.Length == movedAssets.Length)
                {
                    int[] sceneIndices = movedFromAssetPaths
                        .Where(IsSceneFile)
                        .Select((assetPath, index) => index)
                        .ToArray();

                    isSceneInvolved = sceneIndices != null && sceneIndices.Length > 0;

                    if (isSceneInvolved)
                    {
                        string currentProject = CurrentProject;
                        if (!string.IsNullOrEmpty(currentProject))
                        {
                            Dictionary<string, List<string>> projectScenes = LoadProjectScenePaths(true);
                            List<string> scenePaths = projectScenes[currentProject];

                            bool isListChanged = false;
                            for (int i = sceneIndices.Length - 1; i >= 0; i--)
                            {
                                int sceneRank = scenePaths.IndexOf(movedFromAssetPaths[i]);
                                if (sceneRank >= 0)
                                {
                                    scenePaths[sceneRank] = movedAssets[i];
                                    isListChanged = true;
                                }
                            }

                            if (isListChanged)
                            {
                                projectScenes[currentProject] = scenePaths;
                                SaveProjectScenePaths(projectScenes);
                            }
                        }
                    }
                }

                if (!isSceneInvolved && importedAssets != null && importedAssets.Length > 0)
                {
                    isSceneInvolved = importedAssets.Any(IsSceneFile);
                }

                if (!isSceneInvolved && deletedAssets != null && deletedAssets.Length > 0)
                {
                    isSceneInvolved = deletedAssets.Any(IsSceneFile);
                }

                if (isSceneInvolved)
                {
                    OnSceneChanged();
                }
            }
        }

        protected static string GetCurrentScenePath()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.path : string.Empty;
        }

        protected static void OpenScene(string scenePath, bool isModeAdditive)
        {
            if (EditorApplication.isPlaying && scenePath != null)
            {
                var ignoreCase = System.StringComparison.OrdinalIgnoreCase;
                string sceneName = scenePath;

                if (sceneName.EndsWith(SceneFileExtension, ignoreCase))
                {
                    sceneName = sceneName.Substring(0, sceneName.Length - SceneFileExtension.Length);
                }

                if (sceneName.StartsWith("Assets/", ignoreCase) || sceneName.StartsWith(@"Assets\", ignoreCase))
                {
                    sceneName = sceneName.Substring(7);
                }

                var sceneMode = isModeAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
                SceneManager.LoadScene(sceneName, sceneMode);
            }
            else
            {
                var sceneMode = isModeAdditive ? OpenSceneMode.Additive : OpenSceneMode.Single;
                EditorSceneManager.OpenScene(scenePath, sceneMode);
            }
        }

        protected static bool SaveCurrentModifiedScenesIfUserWantsTo()
        {
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        protected static void RepaintAllViews()
        {
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }
    }
}
