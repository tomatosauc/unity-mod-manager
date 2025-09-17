﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using HarmonyLib;

namespace UnityModManagerNet
{
    public partial class UnityModManager
    {
        public partial class UI : MonoBehaviour
        {
            internal static bool Load()
            {
                if (Instance) return true;

                try
                {
                    new GameObject(typeof(UI).FullName, typeof(UI));

                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return false;
            }

            private static UI mInstance = null;

            public static UI Instance
            {
                get { return mInstance; }
            }

            public static GUIStyle window = null;
            public static GUIStyle h1 = null;
            public static GUIStyle h2 = null;
            public static GUIStyle bold = null;
            /// <summary>
            /// [0.13.1]
            /// </summary>
            public static GUIStyle button = null;
            private static GUIStyle settings = null;
            private static GUIStyle status = null;
            private static GUIStyle www = null;
            private static GUIStyle updates = null;
            private static GUIStyle question = null;

            private static GUIStyle tooltipBox = null;

            private bool mFirstLaunched = false;
            private bool mInit = false;

            private bool mOpened = false;
            public bool Opened { get { return mOpened; } }

            private Rect mWindowRect = new Rect(0, 0, 0, 0);
            private Vector2 mWindowSize = Vector2.zero;
            private Vector2 mExpectedWindowSize = Vector2.zero;
            private Resolution mCurrentResolution;

            private GUIContent mTooltip = null;

            private float mUIScale = 1f;
            private float mExpectedUIScale = 1f;
            private bool mUIScaleChanged;

            private string[] mOSfonts = null;
            private string mDefaultFont = "Arial";
            private int mSelectedFont;
            private string mModFilter = "";

            public int globalFontSize = 13;

            private void Awake()
            {
                Logger.Log("Spawning.");

                mInstance = this;
                DontDestroyOnLoad(this);
                mWindowSize = ClampWindowSize(new Vector2(Params.WindowWidth, Params.WindowHeight));
                mExpectedWindowSize = mWindowSize;
                mUIScale = Mathf.Clamp(Params.UIScale, 0.5f, 5f);
                mExpectedUIScale = mUIScale;
                Textures.Init();
                
                mOSfonts = Font.GetOSInstalledFontNames();
                if (mOSfonts.Length == 0)
                {
                    Logger.Error("No compatible font found in OS. If you play through Wine, install winetricks allfonts.");
                    OpenUnityFileLog();
                }
                else
                {
                    if (string.IsNullOrEmpty(Params.UIFont))
                        Params.UIFont = mDefaultFont;
                    if (!mOSfonts.Contains(Params.UIFont))
                        Params.UIFont = mOSfonts.First();

                    mSelectedFont = Array.IndexOf(mOSfonts, Params.UIFont);
                }

                var harmony = new HarmonyLib.Harmony("UnityModManager.UI");
                var original = typeof(Screen).GetMethod("set_lockCursor");
                var prefix = typeof(Screen_lockCursor_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(original, new HarmonyMethod(prefix));
            }

            private void Start()
            {
                CalculateWindowPos();
                if (string.IsNullOrEmpty(Config.UIStartingPoint))
                {
                    FirstLaunch();
                }
                if (Params.CheckUpdates == 2 || Params.CheckUpdates == 1 && Params.LastUpdateCheck.DayOfYear != DateTime.Now.DayOfYear)
                {
                    CheckModUpdates();
                }
            }

            private void OnDestroy()
            {
                Logger.Log("Destroying when exit.");
                SaveSettingsAndParams();
                Logger.WriteBuffers();
            }

            private void Update()
            {
                if (Opened)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                try
                {
                    KeyBinding.BindKeyboard();
                }
                catch (Exception e)
                {
                    Logger.LogException("BindKeyboard", e);
                }

                var deltaTime = Time.deltaTime;
                foreach (var mod in modEntries)
                {
                    if (mod.Active && mod.OnUpdate != null)
                    {
                        try
                        {
                            mod.OnUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.LogException("OnUpdate", e);
                        }
                    }
                }

                if (Params.Hotkey.Up() || Param.DefaultHotkey.Up())
                {
                    ToggleWindow();
                }

                if (mOpened && Param.EscapeHotkey.Up())
                {
                    ToggleWindow();
                }

                foreach (var mod in modEntries)
                {
                    if (mod.Active && mod.Hotkey.Up())
                    {
                        if (tabId == 0 && mOpened == true && mModFilter == mod.Info.DisplayName)
                        {
                            ToggleWindow(false);
                        }
                        else
                        {
                            tabId = 0;
                            mModFilter = mod.Info.DisplayName;
                            ToggleWindow(true);
                            var index = modEntries.FindIndex(x => x.Info.Id == mod.Info.Id);
                            if (index >= 0 && ShowModSettings != index)
                            {
                                ShowModSettings = index;
                            }
                        }
                        
                        break;
                    }
                }
            }

            private void FixedUpdate()
            {
                var deltaTime = Time.fixedDeltaTime;
                foreach (var mod in modEntries)
                {
                    if (mod.Active && mod.OnFixedUpdate != null)
                    {
                        try
                        {
                            mod.OnFixedUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.LogException("OnFixedUpdate", e);
                        }
                    }
                }
            }

            private void LateUpdate()
            {
                var deltaTime = Time.deltaTime;
                foreach (var mod in modEntries)
                {
                    if (mod.Active && mod.OnLateUpdate != null)
                    {
                        try
                        {
                            mod.OnLateUpdate.Invoke(mod, deltaTime);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.LogException("OnLateUpdate", e);
                        }
                    }
                }

                Logger.Watcher(deltaTime);
            }

            private void PrepareGUI()
            {
                window = new GUIStyle();
                window.name = "umm window";
                window.normal.background = Textures.Window;
                window.normal.background.wrapMode = TextureWrapMode.Repeat;

                h1 = new GUIStyle();
                h1.name = "umm h1";
                h1.normal.textColor = Color.white;
                h1.fontStyle = FontStyle.Bold;
                h1.alignment = TextAnchor.MiddleCenter;
                h1.clipping = TextClipping.Overflow;

                h2 = new GUIStyle();
                h2.name = "umm h2";
                h2.normal.textColor = new Color(0.6f, 0.91f, 1f);
                h2.fontStyle = FontStyle.Bold;
                h2.clipping = TextClipping.Overflow;

                bold = new GUIStyle(GUI.skin.label);
                bold.name = "umm bold";
                bold.normal.textColor = Color.white;
                bold.fontStyle = FontStyle.Bold;
                bold.clipping = TextClipping.Overflow;

                button = new GUIStyle(GUI.skin.button);
                button.name = "umm button";
                button.clipping = TextClipping.Overflow;

                settings = new GUIStyle();
                settings.alignment = TextAnchor.MiddleCenter;
                settings.stretchHeight = true;

                status = new GUIStyle();
                status.alignment = TextAnchor.MiddleCenter;
                status.stretchHeight = true;

                www = new GUIStyle();
                www.alignment = TextAnchor.MiddleCenter;
                www.stretchHeight = true;

                updates = new GUIStyle();
                updates.alignment = TextAnchor.MiddleCenter;
                updates.stretchHeight = true;

                question = new GUIStyle();

                tooltipBox = new GUIStyle();
                tooltipBox.alignment = TextAnchor.MiddleCenter;
                tooltipBox.normal.textColor = Color.white;
                tooltipBox.normal.background = Textures.Tooltip;
                tooltipBox.normal.background.SetPixels32(new Color32[4].Select(x => x = new Color32(30, 30, 30, 255)).ToArray());
                tooltipBox.normal.background.Apply();
                tooltipBox.hover = tooltipBox.normal;
                tooltipBox.border.left = 2;
                tooltipBox.border.right = 2;
                tooltipBox.border.top = 2;
                tooltipBox.border.bottom = 2;
                tooltipBox.richText = true;
            }

            private void ScaleGUI()
            {
                GUI.skin.font = Font.CreateDynamicFontFromOSFont(Params.UIFont, Scale(globalFontSize));
                GUI.skin.label.clipping = TextClipping.Overflow;
                GUI.skin.button.padding = new RectOffset(Scale(10), Scale(10), Scale(3), Scale(3));
                //GUI.skin.button.margin = RectOffset(Scale(4), Scale(2));

                GUI.skin.horizontalSlider.fixedHeight = Scale(12);
                GUI.skin.horizontalSlider.border = RectOffset(3, 0);
                GUI.skin.horizontalSlider.padding = RectOffset(Scale(-1), 0);
                GUI.skin.horizontalSlider.margin = RectOffset(Scale(4), Scale(8));

                GUI.skin.horizontalSliderThumb.fixedHeight = Scale(12);
                GUI.skin.horizontalSliderThumb.border = RectOffset(4, 0);
                GUI.skin.horizontalSliderThumb.padding = RectOffset(Scale(7), 0);
                GUI.skin.horizontalSliderThumb.margin = RectOffset(0);

                GUI.skin.toggle.margin.left = Scale(10);

                window.padding = RectOffset(Scale(5));
                h1.fontSize = Scale(16);
                h1.margin = RectOffset(0, Scale(5));
                h2.fontSize = Scale(13);
                h2.margin = RectOffset(0, Scale(3));
                button.fontSize = Scale(13);
                button.padding = RectOffset(Scale(30), Scale(5));

                int iconHeight = 28;
                settings.fixedWidth = Scale(24);
                settings.fixedHeight = Scale(iconHeight);
                status.fixedWidth = Scale(12);
                status.fixedHeight = Scale(iconHeight);
                www.fixedWidth = Scale(24);
                www.fixedHeight = Scale(iconHeight);
                updates.fixedWidth = Scale(26);
                updates.fixedHeight = Scale(iconHeight);
                question.fixedWidth = Scale(10);
                question.fixedHeight = Scale(11);
                question.margin = RectOffset(0, 9);

                mColumns.Clear();
                foreach (var column in mOriginColumns)
                {
                    mColumns.Add(new Column { name = column.name, width = Scale(column.width), expand = column.expand, skip = column.skip });
                }
            }

            private void OnGUI()
            {
                if (!mInit)
                {
                    mInit = true;
                    PrepareGUI();
                    ScaleGUI();
                }

                bool anyRendered = false;
                if (mPopupList.Count > 0)
                {
                    var toRemove = new List<PopupWindow>(0);
                    foreach (var item in mPopupList)
                    {
                        item.DestroyCounter.Add(Time.frameCount);
                        if (item.DestroyCounter.Count > 1)
                        {
                            toRemove.Add(item);
                            continue;
                        }
                        if (item.Opened && !anyRendered)
                        {
                            item.Render();
                            anyRendered = true;
                        }
                    }
                    foreach (var item in toRemove)
                    {
                        mPopupList.Remove(item);
                    }
                }

                if (Window_GUI.mList.Count > 0)
                {
                    foreach (var item in Window_GUI.mList)
                    {
                        if (item.Opened)
                        {
                            item.Render();
                        }
                    }
                }

                if (mOpened)
                {
                    if (mCurrentResolution.width != Screen.currentResolution.width || mCurrentResolution.height != Screen.currentResolution.height)
                    {
                        mCurrentResolution = Screen.currentResolution;
                        CalculateWindowPos();
                    }
                    if (mUIScaleChanged)
                    {
                        mUIScaleChanged = false;
                        ScaleGUI();
                    }
                    var backgroundColor = GUI.backgroundColor;
                    var color = GUI.color;
                    GUI.backgroundColor = Color.white;
                    GUI.color = Color.white;
                    mWindowRect = GUILayout.Window(0, mWindowRect, WindowFunction, "", window, GUILayout.Height(mWindowSize.y));
                    mWindowRect.x = (int)mWindowRect.x;
                    mWindowRect.y = (int)mWindowRect.y;
                    GUI.backgroundColor = backgroundColor;
                    GUI.color = color;
                }

                foreach (var mod in modEntries)
                {
                    if (mod.Active && mod.OnFixedGUI != null)
                    {
                        try
                        {
                            mod.OnFixedGUI.Invoke(mod);
                        }
                        catch (Exception e)
                        {
                            mod.Logger.LogException("OnFixedGUI", e);
                        }
                    }
                }
            }

            public int tabId = 0;
            public string[] tabs = { "Mods", "Logs", "Settings" };

            class Column
            {
                public string name;
                public float width;
                public bool expand = false;
                public bool skip = false;
            }

            private List<Column> mOriginColumns = new List<Column>
            {
                new Column {name = "Name", width = 200, expand = true},
                new Column {name = "Version", width = 60},
                new Column {name = "Requirements", width = 150, expand = true},
                new Column {name = "On/Off", width = 50},
                new Column {name = "Status", width = 50}
            };
            private List<Column> mColumns = new List<Column>();

            private Vector2[] mScrollPosition = new Vector2[0];
            private Vector2 mScrollPositionMax = new Vector2();

            private int mPreviousShowModSettings = -1;
            private int mShowModSettings = -1;
            private int ShowModSettings {
                get { return mShowModSettings; }
                set
                {
                    Action<ModEntry> Hide = (mod) =>
                    {
                        if (mod.Active && mod.OnHideGUI != null && mod.OnGUI != null)
                        {
                            try
                            {
                                mod.OnHideGUI(mod);
                            }
                            catch (ExitGUIException)
                            {
                            }
                            catch (Exception ex)
                            {
                                mod.Logger.LogException("OnHideGUI", ex);
                            }
                        }
                    };

                    Action<ModEntry> Show = (mod) =>
                    {
                        if (mod.Active && mod.OnShowGUI != null && mod.OnGUI != null)
                        {
                            try
                            {
                                mod.OnShowGUI(mod);
                            }
                            catch (ExitGUIException)
                            {
                            }
                            catch (Exception ex)
                            {
                                mod.Logger.LogException("OnShowGUI", ex);
                            }
                        }
                    };

                    mShowModSettings = value;
                    if (mShowModSettings != mPreviousShowModSettings)
                    {
                        if (mShowModSettings == -1)
                        {
                            Hide(modEntries[mPreviousShowModSettings]);
                        }
                        else if (mPreviousShowModSettings == -1)
                        {
                            Show(modEntries[mShowModSettings]);
                        }
                        else 
                        {
                            Hide(modEntries[mPreviousShowModSettings]);
                            Show(modEntries[mShowModSettings]);
                        }
                        mPreviousShowModSettings = mShowModSettings;
                    }
                }
            }

            public static int Scale(int value)
            {
                if (!Instance)
                    return value;
                return (int)(value * Instance.mUIScale);
            }

            public static float Scale(float value)
            {
                if (!Instance)
                    return value;
                return value * Instance.mUIScale;
            }

            private void CalculateWindowPos()
            {
                mWindowSize = ClampWindowSize(mWindowSize);
                mWindowRect = new Rect((Screen.width - (int)mWindowSize.x) / 2, (Screen.height - (int)mWindowSize.y) / 2f, 0, 0);
            }

            private Vector2 ClampWindowSize(Vector2 orig)
            {
                return new Vector2(Mathf.Clamp((int)orig.x, Mathf.Min(960, Screen.width), Screen.width), Mathf.Clamp((int)orig.y, Mathf.Min(720, Screen.height), Screen.height));
            }

            private void WindowFunction(int windowId)
            {
                if (Event.current.type == EventType.Repaint)
                    mTooltip = null;

                if (KeyBinding.Ctrl())
                    GUI.DragWindow(new Rect(0, 0, 10000, 10000));
                GUI.DragWindow(new Rect(0, 0, 10000, 20));

                UnityAction buttons = () => { };

                GUILayout.Label("Mod Manager " + version, h1);

                GUILayout.Space(3);
                GUILayout.BeginHorizontal();
                int tab = tabId;
                tab = GUILayout.Toolbar(tab, tabs, button, GUILayout.ExpandWidth(false));
                if (tab != tabId)
                {
                    tabId = tab;
                }
                GUILayout.FlexibleSpace();
                if (tabId == 0)
                {
                    GUILayout.Label("Filter:");
                    mModFilter = GUILayout.TextField(mModFilter, GUILayout.Width(Scale(150)), GUILayout.Height(Scale(20)));
                    if (GUILayout.Button("X", button, GUILayout.Width(Scale(20)), GUILayout.Height(Scale(20))))
                    {
                        mModFilter = "";
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (mScrollPosition.Length != tabs.Length)
                    mScrollPosition = new Vector2[tabs.Length];

                DrawTab(tabId, ref buttons);

                GUILayout.FlexibleSpace();
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Close", button, GUILayout.ExpandWidth(false)))
                {
                    ToggleWindow();
                }

                if (GUILayout.Button("Save", button, GUILayout.ExpandWidth(false)))
                {
                    SaveSettingsAndParams();
                }

                buttons();
                GUILayout.EndHorizontal();

                if (mTooltip != null && Event.current.type == EventType.Repaint)
                {
                    var size =  tooltipBox.CalcSize(mTooltip) + Vector2.one * 10;
                    var pos = Event.current.mousePosition;
                    if (size.x + pos.x < mWindowRect.width)
                    {
                        GUI.Box(new Rect(pos.x + 20, pos.y - 25, size.x, size.y), mTooltip.text, tooltipBox);
                    }
                    else
                    {
                        GUI.Box(new Rect(pos.x - size.x, pos.y - 25, size.x, size.y), mTooltip.text, tooltipBox);
                    }
                }
                else
                {
                    GUI.Box(new Rect(-9999, 0, 0, 0), "");
                }
            }

            static List<string> mJoinList = new List<string>();

            private void DrawTab(int tabId, ref UnityAction buttons)
            {
                var minWidth = GUILayout.MinWidth(mWindowSize.x);

                switch (tabs[tabId])
                {
                    case "Mods":
                        {
                            mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth, GUILayout.ExpandHeight(false));

                            var amountWidth = mColumns.Where(x => !x.skip).Sum(x => x.width);
                            var expandWidth = mColumns.Where(x => x.expand && !x.skip).Sum(x => x.width);

                            var mods = modEntries;
                            var colWidth = mColumns.Select(x =>
                                x.expand
                                    ? GUILayout.Width(x.width / expandWidth * (mWindowSize.x - 60 + expandWidth - amountWidth))
                                    : GUILayout.Width(x.width)).ToArray();

                            GUILayout.BeginVertical("box");

                            GUILayout.BeginHorizontal("box");
                            for (int i = 0; i < mColumns.Count; i++)
                            {
                                if (mColumns[i].skip)
                                    continue;
                                GUILayout.Label(mColumns[i].name, colWidth[i]);
                            }
                            
                            GUILayout.EndHorizontal();

                            for (int i = 0, c = mods.Count; i < c; i++)
                            {
                                if (!string.IsNullOrEmpty(mModFilter))
                                {
                                    if (!mods[i].Info.DisplayName.ToLower().Contains(mModFilter.ToLower()))
                                    {
                                        continue;
                                    }
                                }
                                int col = -1;
                                GUILayout.BeginVertical("box");
                                GUILayout.BeginHorizontal();

                                GUILayout.BeginHorizontal(colWidth[++col]);
                                if (mods[i].OnGUI != null || mods[i].CanReload)
                                {
                                    if (GUILayout.Button(mods[i].Info.DisplayName, GUI.skin.label, GUILayout.ExpandWidth(true)))
                                    {
                                        ShowModSettings = (ShowModSettings == i) ? -1 : i;
                                    }

                                    if (GUILayout.Button(ShowModSettings == i ? Textures.SettingsActive : Textures.SettingsNormal, settings))
                                    {
                                        ShowModSettings = (ShowModSettings == i) ? -1 : i;
                                    }
                                }
                                else
                                {
                                    GUILayout.Label(mods[i].Info.DisplayName);
                                }

                                if (!string.IsNullOrEmpty(mods[i].Info.HomePage))
                                {
                                    GUILayout.Space(10);
                                    if (GUILayout.Button(Textures.WWW, www))
                                    {
                                        Application.OpenURL(mods[i].Info.HomePage);
                                    }
                                }

                                if (mods[i].NewestVersion != null)
                                {
                                    GUILayout.Space(10);
                                    GUILayout.Box(Textures.Updates, updates);
                                }

                                GUILayout.Space(20);

                                GUILayout.EndHorizontal();

                                GUILayout.BeginHorizontal(colWidth[++col]);
                                GUILayout.Label(mods[i].Info.Version, GUILayout.ExpandWidth(false));
                                //                            if (string.IsNullOrEmpty(mods[i].Info.Repository))
                                //                            {
                                //                                GUI.color = new Color32(255, 81, 83, 255);
                                //                                GUILayout.Label("*");
                                //                                GUI.color = Color.white;
                                //                            }
                                GUILayout.EndHorizontal();

                                if (mods[i].ManagerVersion > GetVersion())
                                {
                                    GUILayout.Label("<color=\"#CD5C5C\">Manager-" + mods[i].Info.ManagerVersion + "</color>", colWidth[++col]);
                                }
                                else if (gameVersion != VER_0 && mods[i].GameVersion > gameVersion)
                                {
                                    GUILayout.Label("<color=\"#CD5C5C\">Game-" + mods[i].Info.GameVersion + "</color>", colWidth[++col]);
                                }
                                else if (mods[i].Requirements.Count > 0)
                                {
                                    GUILayout.BeginHorizontal(colWidth[++col]);
                                    mJoinList.Clear();
                                    foreach (var item in mods[i].Requirements)
                                    {
                                        var id = item.Key;
                                        var mod = FindMod(id);
                                        mJoinList.Add(((mod == null || item.Value != null && item.Value > mod.Version || !mod.Active) && mods[i].Active) ? "<color=\"#CD5C5C\">" + id + "</color> " : id);
                                    }
                                    GUILayout.Label(string.Join(", ", mJoinList.ToArray()));
                                    GUILayout.EndHorizontal();
                                }
                                else if (!string.IsNullOrEmpty(mods[i].CustomRequirements))
                                {
                                    GUILayout.Label(mods[i].CustomRequirements, colWidth[++col]);
                                }
                                else
                                {
                                    GUILayout.Label("-", colWidth[++col]);
                                }

                                if (!forbidDisableMods)
                                {
                                    var action = mods[i].Enabled;
                                    action = GUILayout.Toggle(action, "", colWidth[++col]);
                                    if (action != mods[i].Enabled)
                                    {
                                        mods[i].Enabled = action;
                                        if (mods[i].Toggleable)
                                            mods[i].Active = action;
                                        else if (action && !mods[i].Loaded)
                                            mods[i].Active = action;
                                    }
                                }
                                else
                                {
                                    GUILayout.Label("", colWidth[++col]);
                                }

                                if (mods[i].Active)
                                {
                                    GUILayout.Box(mods[i].Enabled ? Textures.StatusActive : Textures.StatusNeedRestart, status);
                                }
                                else
                                {
                                    GUILayout.Box(!mods[i].Enabled ? Textures.StatusInactive : Textures.StatusNeedRestart, status);
                                }
                                if (mods[i].ErrorOnLoading)
                                    GUILayout.Label("!!!");

                                GUILayout.EndHorizontal();

                                if (ShowModSettings == i)
                                {
                                    if (mods[i].CanReload)
                                    {
                                        GUILayout.Label("Debug", h2);
                                        if (GUILayout.Button("Reload", button, GUILayout.ExpandWidth(false)))
                                        {
                                            mods[i].Reload();
                                        }
                                        GUILayout.Space(5);
                                    }
                                    if (mods[i].Active && mods[i].OnGUI != null)
                                    {
                                        GUILayout.Label("Options", h2);
                                        try
                                        {
                                            mods[i].OnGUI(mods[i]);
                                        }
                                        //catch (ExitGUIException e)
                                        //{
                                        //    throw e;
                                        //}
                                        catch (Exception e)
                                        {
                                            mods[i].Logger.LogException("OnGUI", e);
                                            ShowModSettings = -1;
                                            GUIUtility.ExitGUI();
                                        }
                                    }
                                }

                                GUILayout.EndVertical();
                            }

                            GUILayout.EndVertical();

                            GUILayout.EndScrollView();

                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.SettingsNormal, settings);
                            GUILayout.Space(3);
                            GUILayout.Label("Options", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.WWW, www);
                            GUILayout.Space(3);
                            GUILayout.Label("Home page", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.Updates, updates);
                            GUILayout.Space(3);
                            GUILayout.Label("Available update", GUILayout.ExpandWidth(false));
                            GUILayout.Space(15);
                            GUILayout.Box(Textures.StatusActive, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Active", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.StatusInactive, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Inactive", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Box(Textures.StatusNeedRestart, status);
                            GUILayout.Space(3);
                            GUILayout.Label("Need restart", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Label("!!!", GUILayout.ExpandWidth(false));
                            GUILayout.Space(3);
                            GUILayout.Label("Errors", GUILayout.ExpandWidth(false));
                            GUILayout.Space(10);
                            GUILayout.Label("[CTRL + LClick]", bold, GUILayout.ExpandWidth(false));
                            GUILayout.Space(3);
                            GUILayout.Label("Drag window", GUILayout.ExpandWidth(false));
                            //                        GUILayout.Space(10);
                            //                        GUI.color = new Color32(255, 81, 83, 255);
                            //                        GUILayout.Label("*", bold, GUILayout.ExpandWidth(false));
                            //                        GUI.color = Color.white;
                            //                        GUILayout.Space(3);
                            //                        GUILayout.Label("Not support updates", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            if (GUI.changed)
                            {
                            }

                            break;
                        }

                    case "Logs":
                        {
                            var scrollToBottom = false;
                            if (Event.current.type == EventType.Repaint)
                            {
                                scrollToBottom = mScrollPositionMax == mScrollPosition[tabId];
                            }

                            mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth);

                            GUILayout.BeginVertical("box");

                            for (int c = Logger.history.Count, i = Mathf.Max(0, c - Logger.historyCapacity); i < c; i++)
                            {
                                GUILayout.Label(Logger.history[i]);
                            }

                            GUILayout.EndVertical();

                            var verticalHeight = 0f;
                            if (Event.current.type == EventType.Repaint)
                            {
                                Rect r = GUILayoutUtility.GetLastRect();
                                verticalHeight = r.height + r.y * 2;
                            }

                            GUILayout.EndScrollView();

                            if (Event.current.type == EventType.Repaint)
                            {
                                Rect r = GUILayoutUtility.GetLastRect();
                                mScrollPositionMax = new Vector2(0, verticalHeight - r.height);

                                if (scrollToBottom)
                                {
                                    mScrollPosition[tabId] = mScrollPositionMax;
                                }
                            }

                            buttons += delegate
                            {
                                if (GUILayout.Button("Clear", button, GUILayout.ExpandWidth(false)))
                                {
                                    Logger.Clear();
                                }
                                if (GUILayout.Button("Open detailed log", button, GUILayout.ExpandWidth(false)))
                                {
                                    OpenUnityFileLog();
                                }
                            };

                            break;
                        }

                    case "Settings":
                        {
                            mScrollPosition[tabId] = GUILayout.BeginScrollView(mScrollPosition[tabId], minWidth);

                            GUILayout.BeginVertical("box");

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Hotkey (default Ctrl+F10)", GUILayout.ExpandWidth(false));
                            DrawKeybindingSmart(Params.Hotkey, "UMM Hotkey", null, GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Check updates", GUILayout.ExpandWidth(false));
                            ToggleGroup(Params.CheckUpdates, mCheckUpdateStrings, i => { Params.CheckUpdates = i; }, null, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Show this window on startup", GUILayout.ExpandWidth(false));
                            ToggleGroup(Params.ShowOnStart, mShowOnStartStrings, i => { Params.ShowOnStart = i; }, null, GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();

                            GUILayout.Space(5);

                            GUILayout.BeginVertical("box");
                            GUILayout.Label("Window size", bold, GUILayout.ExpandWidth(false));
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Width ", GUILayout.ExpandWidth(false));
                            mExpectedWindowSize.x = GUILayout.HorizontalSlider(mExpectedWindowSize.x, Mathf.Min(Screen.width, 960), Screen.width, GUILayout.Width(200));
                            GUILayout.Label(" " + mExpectedWindowSize.x.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Height", GUILayout.ExpandWidth(false));
                            mExpectedWindowSize.y = GUILayout.HorizontalSlider(mExpectedWindowSize.y, Mathf.Min(Screen.height, 720), Screen.height, GUILayout.Width(200));
                            GUILayout.Label(" " + mExpectedWindowSize.y.ToString("f0") + " px ", GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
                            {
                                mWindowSize.x = (int)mExpectedWindowSize.x;
                                mWindowSize.y = (int)mExpectedWindowSize.y;
                                CalculateWindowPos();
                                Params.WindowWidth = mWindowSize.x;
                                Params.WindowHeight = mWindowSize.y;
                            }
                            GUILayout.EndVertical();

                            GUILayout.Space(5);

                            GUILayout.BeginVertical("box");
                            GUILayout.Label("UI", bold, GUILayout.ExpandWidth(false));
                            GUILayout.Label("Font", GUILayout.ExpandWidth(false));
                            PopupToggleGroup(ref mSelectedFont, mOSfonts, null, GUI.skin.button, GUILayout.Width(200));
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Scale", GUILayout.ExpandWidth(false));
                            mExpectedUIScale = GUILayout.HorizontalSlider(mExpectedUIScale, 0.5f, 5f, GUILayout.Width(200));
                            GUILayout.Label(" " + mExpectedUIScale.ToString("f2"), GUILayout.ExpandWidth(false));
                            GUILayout.EndHorizontal();
                            if (GUILayout.Button("Apply", button, GUILayout.ExpandWidth(false)))
                            {
                                if (mUIScale != mExpectedUIScale || mOSfonts[mSelectedFont] != Params.UIFont)
                                {
                                    mUIScaleChanged = true;
                                    mUIScale = mExpectedUIScale;
                                    Params.UIScale = mUIScale;
                                    Params.UIFont = mOSfonts[mSelectedFont];
                                }
                            }
                            GUILayout.EndVertical();

                            GUILayout.Space(5);

                            GUILayout.BeginVertical("box");
                            GUILayout.Label("Mods Hotkeys", bold, GUILayout.ExpandWidth(false));
                            var mods = modEntries;
                            for (int i = 0, c = mods.Count; i < c; i++)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label($"{mods[i].Info.DisplayName}", GUILayout.Width(200));
                                DrawKeybindingSmart(mods[i].Hotkey, "Hotkey", null, GUILayout.ExpandWidth(false));
                                GUILayout.EndHorizontal();
                                GUILayout.Space(5);
                            }
                            GUILayout.EndVertical();
                            GUILayout.EndVertical();
                            GUILayout.EndScrollView();

                            break;
                        }
                }
            }

            private static string[] mCheckUpdateStrings = { "Disabled", "Once a day", "Everytime" };
            
            private static string[] mShowOnStartStrings = { "No", "Yes" };

            [Obsolete]
            private static string[] mHotkeyNames = { "CTRL+F10", "ScrollLock", "Num *", "~" };

            internal bool GameCursorVisible { get; set; }
            internal CursorLockMode GameCursorLockMode { get; set; }

            public void FirstLaunch()
            {
                if (mFirstLaunched || UnityModManager.Params.ShowOnStart == 0 && modEntries.All(x => !x.ErrorOnLoading))
                    return;

                ToggleWindow(true);
            }

            public void ToggleWindow()
            {
                ToggleWindow(!mOpened);
            }

            public void ToggleWindow(bool open)
            {
                if (open == mOpened)
                    return;

                if (open)
                    mFirstLaunched = true;

                if (!open)
                {
                    var i = ShowModSettings;
                    ShowModSettings = -1;
                    mShowModSettings = i;
                }
                else
                {
                    ShowModSettings = mShowModSettings;
                }

                try
                {
                    mOpened = open;
                    BlockGameUI(open);
                    //if (!open)
                    //    SaveSettingsAndParams();
                    if (open)
                    {
                        GameCursorLockMode = Cursor.lockState;
                        GameCursorVisible = Cursor.visible;
                        
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }
                    else
                    {
                        Cursor.visible = GameCursorVisible;
                        Cursor.lockState = GameCursorLockMode;
                    }
                    GameScripts.OnToggleWindow(open);
                }
                catch (Exception e)
                {
                    Logger.LogException("ToggleWindow", e);
                }
            }

            private GameObject mCanvas = null;

            private void BlockGameUI(bool value)
            {
                if (value)
                {
                    mCanvas = new GameObject("UMM blocking UI", typeof(Canvas), typeof(GraphicRaycaster));
                    var canvas = mCanvas.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = Int16.MaxValue;
                    DontDestroyOnLoad(mCanvas);
                    var panel = new GameObject("Image", typeof(Image));
                    panel.transform.SetParent(mCanvas.transform);
                    var rect = panel.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
                }
                else
                {
                    if (mCanvas)
                        Destroy(mCanvas);
                }
            }

            private static RectOffset RectOffset(int value)
            {
                return new RectOffset(value, value, value, value);
            }

            private static RectOffset RectOffset(int x, int y)
            {
                return new RectOffset(x, x, y, y);
            }

            private static int mLastWindowId = 0;

            public static int GetNextWindowId()
            {
                return ++mLastWindowId;
            }

            /// <summary>
            /// Renders question mark with a tooltip [0.25.0]
            /// </summary>
            public static void RenderTooltip(string str, GUIStyle style = null, params GUILayoutOption[] options)
            {
                BeginTooltip(str);
                EndTooltip(str, style, options);
            }

            /// <summary>
            /// Call after any GUILayout [0.28.2]
            /// </summary>
            public static void Tooltip(string str)
            {
                if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    ShowTooltip(str);
                }
            }
        }

        //        [HarmonyPatch(typeof(Screen), "lockCursor", MethodType.Setter)]
        static class Screen_lockCursor_Patch
        {
            static bool Prefix(bool value)
            {
                if (UI.Instance != null && UI.Instance.Opened)
                {
                    if (value)
                    {
                        UI.Instance.GameCursorVisible = false;
                        UI.Instance.GameCursorLockMode = CursorLockMode.Locked;
                    }
                    else
                    {
                        UI.Instance.GameCursorLockMode = CursorLockMode.None;
                        UI.Instance.GameCursorVisible = true;
                    }
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    return false;
                }

                return true;
            }
        }
    }
}

