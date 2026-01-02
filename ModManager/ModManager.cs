using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Factory;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MiniMotorwaysModLoader;
using System.Linq;
using Motorways.Views;
using Motorways.UI;
using Screens;

namespace ModManagerMod
{
    /// <summary>
    /// Main Mod Manager - adds UI to enable/disable mods
    /// </summary>
    public class ModManager : IMod
    {
        public string Name => "Mod Manager";
        public string Version => "0.1.0-beta";
        public string Author => "Roofus";
        
        private ModManagerUI _ui;
        private GameObject _modsButton;
        private bool _isInitialized = false;
        
        public void OnLoad()
        {
            Debug.Log("[ModManager] Initializing Mod Manager...");
            
            // Create UI GameObject
            var uiObject = new GameObject("ModManagerUI");
            _ui = uiObject.AddComponent<ModManagerUI>();
            
            // Don't destroy on load
            UnityEngine.Object.DontDestroyOnLoad(uiObject);
            
            // Initialize native UI in a coroutine
            var initHelper = uiObject.AddComponent<NativeUIInitializer>();
            initHelper.Initialize(this);
            
            Debug.Log("[ModManager] OK");
        }
        
        public void OnUnload()
        {
            if (_ui != null)
            {
                UnityEngine.Object.Destroy(_ui.gameObject);
            }
            
            if (_modsButton != null)
            {
                UnityEngine.Object.Destroy(_modsButton);
            }
        }
        
        private void CreateModsButton(MainMenuScreen mainMenu)
        {
            if (_isInitialized) return;
            
            try
            {
                if (mainMenu == null)
                {
                    Debug.LogError("[ModManager] MainMenuScreen is null, cannot add Mods button");
                    return;
                }

                // Find the Options button to use as a template (prefer the serialized field)
                GameObject optionsButtonObject = null;
                var optionsButtonField = typeof(MainMenuScreen).GetField("_optionsButton",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (optionsButtonField != null)
                {
                    optionsButtonObject = optionsButtonField.GetValue(mainMenu) as GameObject;
                }

                if (optionsButtonObject == null)
                {
                    foreach (var candidate in mainMenu.GetComponentsInChildren<TouchButton>(true))
                    {
                        if (candidate.name.IndexOf("Options", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            optionsButtonObject = candidate.gameObject;
                            break;
                        }
                    }
                }

                var optionsButton = optionsButtonObject != null ? optionsButtonObject.GetComponent<TouchButton>() : null;
                if (optionsButton == null)
                {
                    Debug.LogError("[ModManager] Could not find Options button to clone for Mods button");
                    return;
                }
                
                // Create the Mods button by cloning the Options button
                _modsButton = UnityEngine.Object.Instantiate(optionsButtonObject, optionsButtonObject.transform.parent);
                _modsButton.name = "Button_Mods";
                _modsButton.SetActive(true);
                
                // Position the button below the Options button
                var rectTransform = _modsButton.GetComponent<RectTransform>();
                var optionsRect = optionsButtonObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.SetSiblingIndex(optionsButtonObject.transform.GetSiblingIndex() + 1);
                    var parentLayout = optionsButtonObject.transform.parent != null
                        ? optionsButtonObject.transform.parent.GetComponent<LayoutGroup>()
                        : null;
                    if (parentLayout == null && optionsRect != null)
                    {
                        rectTransform.anchoredPosition = new Vector2(
                            optionsRect.anchoredPosition.x,
                            optionsRect.anchoredPosition.y - optionsRect.sizeDelta.y - 20f // 20px spacing
                        );
                    }
                }
                
                // Update the button text
                var localizedText = _modsButton.GetComponentInChildren<LocalizedTextUI>(true);
                if (localizedText != null)
                {
                    localizedText.ignoreLocalization = true;
                    if (localizedText.TextField != null)
                    {
                        localizedText.TextField.text = "Mods";
                    }
                }
                else
                {
                    var tmpText = _modsButton.GetComponentInChildren<TMP_Text>(true);
                    if (tmpText != null)
                    {
                        tmpText.text = "Mods";
                    }
                    else
                    {
                        var uiText = _modsButton.GetComponentInChildren<Text>(true);
                        if (uiText != null)
                        {
                            uiText.text = "Mods";
                        }
                    }
                }
                
                var button = _modsButton.GetComponent<TouchButton>();
                if (button != null)
                {
                    var onClickField = typeof(TouchButton).GetField("_onClick", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (onClickField != null)
                    {
                        onClickField.SetValue(button, new TouchButton.ButtonClickedEvent());
                    }
                    
                    button.AddOnClickedEvent(() => {
                        if (_ui != null)
                        {
                            _ui.ToggleWindow();
                        }
                    });
                }
                
                _isInitialized = true;
                Debug.Log("[ModManager] Added Mods button to main menu");

                if (_ui != null)
                {
                    _ui.InitializeNativeUI(mainMenu, optionsButtonObject, button);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModManager] Error creating Mods button: {ex}");
            }
        }
        
        // Helper class to handle coroutines for UI initialization
        private class NativeUIInitializer : MonoBehaviour
        {
            private ModManager _modManager;
            
            public void Initialize(ModManager modManager)
            {
                _modManager = modManager;
                StartCoroutine(InitializeNativeUI());
            }
            
            private IEnumerator InitializeNativeUI()
            {
                // Wait for the main menu to be loaded
                MainMenuScreen mainMenu = null;
                while (mainMenu == null)
                {
                    mainMenu = FindObjectOfType<MainMenuScreen>();
                    if (mainMenu == null)
                    {
                        yield return new WaitForSeconds(0.5f);
                    }
                }
                
                _modManager.CreateModsButton(mainMenu);
            }
        }
    }
    
    /// <summary>
   /// Simple IMGUI-based Mod Manager UI
    /// </summary>
    public class ModManagerUI : MonoBehaviour
    {
        private const bool UseTemplateBackground = false;
        private const string DisabledSuffix = ".disabled";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private ModConfig _config;
        private Dictionary<string, bool> _modStates = new Dictionary<string, bool>();
        private List<string> _lastScanPaths = new List<string>();
        private List<string> _lastSearchRoots = new List<string>();
        private bool _hasChanges = false;
        private MainMenuScreen _mainMenu;
        private GameObject _buttonTemplate;
        private TMP_Text _textTemplate;
        private Text _legacyTextTemplate;
        private IScope _appScope;
        private GameObject _nativeRoot;
        private CanvasGroup _nativeCanvasGroup;
        private RectTransform _nativeListContent;
        private TMP_Text _nativeCountText;
        private Text _nativeCountLegacyText;
        private TMP_Text _nativeStatusText;
        private Text _nativeStatusLegacyText;
        private readonly Dictionary<string, string> _logicalToActualPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _disabledOnDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Config panel fields
        private GameObject _configPanelRoot;
        private CanvasGroup _configPanelCanvasGroup;
        private RectTransform _configListContent;
        private string _currentConfigModName;
        private IConfigurableMod _currentConfigMod;
        private List<ModConfigEntry> _currentConfigEntries = new List<ModConfigEntry>();
        private Dictionary<string, GameObject> _configInputFields = new Dictionary<string, GameObject>();
        
        public void ToggleWindow()
        {
            if (!EnsureNativeUI())
            {
                return;
            }

            bool shouldShow = !_nativeCanvasGroup.interactable;
            if (shouldShow)
            {
                RefreshModList();
                RefreshNativeList();
            }
            SetNativeVisible(shouldShow);
        }
        
        void Start()
        {
            _config = ModConfig.Load();
            RefreshModList();
        }
        
        void RefreshModList()
        {
            _modStates.Clear();
            _lastSearchRoots = BuildSearchRoots();
            var allMods = GetAllModPathsSafe();
            _lastScanPaths = new List<string>(allMods);
            foreach (var modPath in allMods)
            {
                bool enabled = !_disabledOnDisk.Contains(modPath);
                _modStates[modPath] = enabled;
                if (!enabled && !_config.IsModDisabled(modPath))
                {
                    _config.SetModEnabled(modPath, enabled: false);
                }
            }

            if (_nativeRoot != null)
            {
                RefreshNativeList();
            }
        }

        public void InitializeNativeUI(MainMenuScreen mainMenu, GameObject buttonTemplate, TouchButton modsButton = null)
        {
            _mainMenu = mainMenu;
            _buttonTemplate = buttonTemplate;
            _appScope = GetAppScope(mainMenu);
            CacheTextTemplate(buttonTemplate);
            if (modsButton != null)
            {
                InitializeButton(modsButton);
            }
            EnsureNativeUI();
        }

        private void CacheTextTemplate(GameObject buttonTemplate)
        {
            if (buttonTemplate == null)
            {
                return;
            }

            var localized = buttonTemplate.GetComponentInChildren<LocalizedTextUI>(true);
            if (localized != null && localized.TextField != null)
            {
                _textTemplate = localized.TextField;
                return;
            }

            _textTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
            if (_textTemplate == null)
            {
                _legacyTextTemplate = buttonTemplate.GetComponentInChildren<Text>(true);
            }
        }

        private bool EnsureNativeUI()
        {
            if (_nativeRoot != null)
            {
                return true;
            }

            if (_mainMenu == null || _buttonTemplate == null)
            {
                return false;
            }

            BuildNativeUI();
            return _nativeRoot != null;
        }

        private void BuildNativeUI()
        {
            var parent = _mainMenu.transform.parent != null ? _mainMenu.transform.parent : _mainMenu.transform;

            _nativeRoot = new GameObject("ModManagerScreen");
            _nativeRoot.transform.SetParent(parent, false);
            _nativeRoot.transform.SetAsLastSibling();
            var rootRect = _nativeRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _nativeCanvasGroup = _nativeRoot.AddComponent<CanvasGroup>();
            SetNativeVisible(false);

            var background = new GameObject("Background");
            background.transform.SetParent(_nativeRoot.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = background.AddComponent<Image>();
            Image templateBackground = null;
            if (UseTemplateBackground)
            {
                templateBackground = FindBestBackgroundImage(_mainMenu.transform);
                if (templateBackground == null && _mainMenu.transform.parent != null)
                {
                    templateBackground = FindBestBackgroundImage(_mainMenu.transform.parent);
                }
            }
            if (UseTemplateBackground && templateBackground != null && templateBackground.sprite != null)
            {
                backgroundImage.sprite = templateBackground.sprite;
                backgroundImage.type = templateBackground.type;
                backgroundImage.color = templateBackground.color;
                backgroundImage.material = templateBackground.material;
            }
            else
            {
                backgroundImage.color = new Color(0f, 0f, 0f, 0.7f);
            }

            var contentRoot = new GameObject("Content");
            contentRoot.transform.SetParent(_nativeRoot.transform, false);
            var contentRect = contentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.07f, 0.07f);
            contentRect.anchorMax = new Vector2(0.93f, 0.93f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentRoot.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 16f;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var headerObject = CreateTextObject(contentRoot.transform, "Title", "Mods", 1.1f, TextAnchor.MiddleCenter, out var _, out var _);
            var headerLayout = headerObject.AddComponent<LayoutElement>();
            if (headerLayout != null)
            {
                headerLayout.preferredHeight = 70f;
            }

            var countObject = CreateTextObject(contentRoot.transform, "Count", "Detected DLLs: 0", 0.85f, TextAnchor.MiddleCenter, out _nativeCountText, out _nativeCountLegacyText);
            var countLayout = countObject.AddComponent<LayoutElement>();
            if (countLayout != null)
            {
                countLayout.preferredHeight = 40f;
            }

            var scrollRoot = new GameObject("ModList");
            scrollRoot.transform.SetParent(contentRoot.transform, false);
            var scrollRect = scrollRoot.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            var scrollLayout = scrollRoot.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.minHeight = 220f;
            var scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.15f);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scroll.viewport = viewportRect;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _nativeListContent = content.AddComponent<RectTransform>();
            _nativeListContent.anchorMin = new Vector2(0f, 1f);
            _nativeListContent.anchorMax = new Vector2(1f, 1f);
            _nativeListContent.pivot = new Vector2(0.5f, 1f);
            _nativeListContent.offsetMin = Vector2.zero;
            _nativeListContent.offsetMax = Vector2.zero;
            scroll.content = _nativeListContent;

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.spacing = 8f;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var statusObject = CreateTextObject(contentRoot.transform, "Status", string.Empty, 0.8f, TextAnchor.MiddleCenter, out _nativeStatusText, out _nativeStatusLegacyText);
            var statusLayout = statusObject.AddComponent<LayoutElement>();
            if (statusLayout != null)
            {
                statusLayout.preferredHeight = 36f;
            }

            var buttonsRoot = new GameObject("Buttons");
            buttonsRoot.transform.SetParent(contentRoot.transform, false);
            var buttonsLayout = buttonsRoot.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 12f;
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            var buttonsLayoutElement = buttonsRoot.AddComponent<LayoutElement>();
            buttonsLayoutElement.preferredHeight = 70f;

            CreateFooterButton(buttonsRoot.transform, "Apply & Restart", OnApplyPressed);
            CreateFooterButton(buttonsRoot.transform, "Cancel", OnCancelPressed);
            CreateFooterButton(buttonsRoot.transform, "Close", OnClosePressed);
        }

        private void SetNativeVisible(bool visible)
        {
            if (_nativeCanvasGroup == null)
            {
                return;
            }

            _nativeCanvasGroup.alpha = visible ? 1f : 0f;
            _nativeCanvasGroup.interactable = visible;
            _nativeCanvasGroup.blocksRaycasts = visible;
        }

        private void RefreshNativeList()
        {
            if (_nativeListContent == null)
            {
                return;
            }

            for (int i = _nativeListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_nativeListContent.GetChild(i).gameObject);
            }

            var allMods = _lastScanPaths.Count > 0 ? _lastScanPaths : GetAllModPathsSafe();
            SetLabelText(_nativeCountText, _nativeCountLegacyText, $"Detected DLLs: {allMods.Count}");

            if (allMods.Count == 0)
            {
                CreateInfoLabel(_nativeListContent, "No mod DLLs detected. Expected layout: Mods/TimerMod/TimerMod.dll");
                if (_lastSearchRoots.Count > 0)
                {
                    CreateInfoLabel(_nativeListContent, "Search roots:");
                    foreach (var root in _lastSearchRoots)
                    {
                        CreateInfoLabel(_nativeListContent, root);
                    }
                }
                UpdateNativeStatus();
                return;
            }

            var loadedMods = ModLoaderEntry.GetLoadedMods();
            foreach (var modPath in allMods)
            {
                string modName = Path.GetFileNameWithoutExtension(modPath);

                string displayName = modName;
                string version = "";
                IMod loadedMod = null;

                foreach (var loaded in loadedMods)
                {
                    if (loaded.Name.Replace(" ", "") == modName)
                    {
                        displayName = loaded.Name;
                        version = $" v{loaded.Version}";
                        loadedMod = loaded;
                        break;
                    }
                }

                bool canToggle = modName != "ModManager" && displayName != "Mod Manager";
                bool isEnabled = _modStates.ContainsKey(modPath) ? _modStates[modPath] : true;
                string status = canToggle ? (isEnabled ? "Enabled" : "Disabled") : "Always On";
                
                // Check if mod is configurable using reflection
                bool isConfigurable = false;
                if (loadedMod != null && isEnabled)
                {
                    var modType = loadedMod.GetType();
                    foreach (var iface in modType.GetInterfaces())
                    {
                        if (iface.Name == "IConfigurableMod")
                        {
                            isConfigurable = true;
                            break;
                        }
                    }
                }

                // Add (CFG) marker to display name if configurable
                string fullLabel = $"{displayName}{version} - {status}";
                if (isConfigurable)
                {
                    fullLabel += " (CFG)";
                }

                // Create the mod button using original pattern
                var row = CreateButtonRow(_nativeListContent, fullLabel, () =>
                {
                    if (!canToggle)
                    {
                        return;
                    }

                    bool nextEnabled = !_modStates[modPath];
                    _modStates[modPath] = nextEnabled;
                    _config.SetModEnabled(modPath, nextEnabled);
                    _hasChanges = true;
                    RefreshNativeList();
                }, canToggle);

                SetButtonLabelColor(row, isEnabled ? Color.white : new Color(0.8f, 0.8f, 0.8f, 0.85f));
                
                // Add a separate config button row underneath
                if (isConfigurable)
                {
                    string capturedDisplayName = displayName;
                    IMod capturedMod = loadedMod;
                    
                    var configRow = CreateButtonRow(_nativeListContent, "    > Configure Settings", () =>
                    {
                        OpenConfigPanelViaReflection(capturedDisplayName, capturedMod);
                    }, true);
                    
                    SetButtonLabelColor(configRow, new Color(0.7f, 0.9f, 1f));
                    SetButtonLabelScale(configRow, 0.75f);
                }
            }

            UpdateNativeStatus();
        }

        private void UpdateNativeStatus()
        {
            string status = _hasChanges ? "Restart required to apply changes" : string.Empty;
            SetLabelText(_nativeStatusText, _nativeStatusLegacyText, status);
        }

        private void OnApplyPressed()
        {
            if (_hasChanges)
            {
                ApplyFileChanges();
                _config.Save();
                Debug.Log("[ModManager] Changes saved. Restarting...");
                Application.Quit();
            }
            else
            {
                SetNativeVisible(false);
            }
        }

        private void OnCancelPressed()
        {
            _config = ModConfig.Load();
            RefreshModList();
            _hasChanges = false;
            SetNativeVisible(false);
        }

        private void OnClosePressed()
        {
            SetNativeVisible(false);
        }

        // ===== CONFIG PANEL METHODS =====
        
        // Cached reflection data for the current config mod
        private object _reflectionConfigMod;
        private MethodInfo _getConfigEntriesMethod;
        private MethodInfo _onConfigChangedMethod;
        private MethodInfo _loadConfigMethod;
        
        private void OpenConfigPanelViaReflection(string modName, IMod mod)
        {
            _currentConfigModName = modName;
            _reflectionConfigMod = mod;
            
            var modType = mod.GetType();
            
            // Find the methods via reflection
            _getConfigEntriesMethod = modType.GetMethod("GetConfigEntries");
            _onConfigChangedMethod = modType.GetMethod("OnConfigChanged");
            _loadConfigMethod = modType.GetMethod("LoadConfig");
            
            if (_getConfigEntriesMethod == null)
            {
                Debug.LogError($"[ModManager] {modName} does not have GetConfigEntries method");
                return;
            }
            
            // Load saved values first
            var savedValues = _config.GetModSettings(modName);
            if (_loadConfigMethod != null)
            {
                try
                {
                    _loadConfigMethod.Invoke(mod, new object[] { savedValues });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModManager] Failed to load config: {ex.Message}");
                }
            }
            
            // Get config entries via reflection
            try
            {
                var entriesObj = _getConfigEntriesMethod.Invoke(mod, null);
                _currentConfigEntries = ConvertToModConfigEntries(entriesObj);
                Debug.Log($"[ModManager] Config entries for {modName}: {_currentConfigEntries.Count} entries found");
                foreach (var entry in _currentConfigEntries)
                {
                    Debug.Log($"[ModManager]   - {entry.Key}: {entry.DisplayName} = {entry.CurrentValue} (type: {entry.Type})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModManager] Failed to get config entries: {ex.Message}");
                return;
            }
            
            _currentConfigMod = null; // We're using reflection instead
            BuildConfigPanelReflection();
            SetConfigPanelVisible(true);
        }
        
        private List<ModConfigEntry> ConvertToModConfigEntries(object entriesObj)
        {
            var result = new List<ModConfigEntry>();
            if (entriesObj == null) return result;
            
            // entriesObj is a List<T> where T is the mod's own ModConfigEntry type
            var enumerable = entriesObj as System.Collections.IEnumerable;
            if (enumerable == null) return result;
            
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                
                var itemType = item.GetType();
                var entry = new ModConfigEntry();
                
                // Read properties via reflection
                entry.Key = GetPropertyValue<string>(item, itemType, "Key") ?? "";
                entry.DisplayName = GetPropertyValue<string>(item, itemType, "DisplayName") ?? entry.Key;
                entry.Description = GetPropertyValue<string>(item, itemType, "Description") ?? "";
                entry.DefaultValue = GetPropertyValue<object>(item, itemType, "DefaultValue");
                entry.CurrentValue = GetPropertyValue<object>(item, itemType, "CurrentValue");
                entry.MinValue = GetPropertyValue<object>(item, itemType, "MinValue");
                entry.MaxValue = GetPropertyValue<object>(item, itemType, "MaxValue");
                entry.EnumOptions = GetPropertyValue<string[]>(item, itemType, "EnumOptions");
                
                // Get ConfigType enum value
                var typeVal = GetPropertyValue<object>(item, itemType, "Type");
                if (typeVal != null)
                {
                    entry.Type = (ConfigType)Convert.ToInt32(typeVal);
                }
                
                result.Add(entry);
            }
            
            return result;
        }
        
        private T GetPropertyValue<T>(object obj, Type type, string propertyName)
        {
            var prop = type.GetProperty(propertyName);
            if (prop != null)
            {
                try
                {
                    var val = prop.GetValue(obj, null);
                    if (val is T typedVal) return typedVal;
                    if (val != null && typeof(T) == typeof(object)) return (T)val;
                }
                catch { }
            }
            return default(T);
        }
        
        private void BuildConfigPanelReflection()
        {
            // Reuse the existing BuildConfigPanel but with reflection-aware save
            BuildConfigPanel();
        }

        
        private void OpenConfigPanel(string modName, IConfigurableMod configurableMod)
        {
            _currentConfigModName = modName;
            _currentConfigMod = configurableMod;
            _currentConfigEntries = configurableMod.GetConfigEntries();
            
            // Load saved values
            var savedValues = _config.GetModSettings(modName);
            configurableMod.LoadConfig(savedValues);
            
            // Refresh entries after loading
            _currentConfigEntries = configurableMod.GetConfigEntries();
            
            BuildConfigPanel();
            SetConfigPanelVisible(true);
        }
        
        private void BuildConfigPanel()
        {
            // Destroy existing panel
            if (_configPanelRoot != null)
            {
                Destroy(_configPanelRoot);
            }
            _configInputFields.Clear();
            
            var parent = _nativeRoot.transform.parent;
            
            _configPanelRoot = new GameObject("ModConfigPanel");
            _configPanelRoot.transform.SetParent(parent, false);
            _configPanelRoot.transform.SetAsLastSibling();
            var rootRect = _configPanelRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            
            _configPanelCanvasGroup = _configPanelRoot.AddComponent<CanvasGroup>();
            
            // Background
            var bgObject = new GameObject("Background");
            bgObject.transform.SetParent(_configPanelRoot.transform, false);
            var bgRect = bgObject.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgObject.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.95f);
            
            // Content container - larger panel for better visibility
            float panelWidth = Mathf.Min(800f, Screen.width * 0.85f);
            float panelHeight = Mathf.Min(700f, Screen.height * 0.85f);
            
            var contentRoot = new GameObject("ContentRoot");
            contentRoot.transform.SetParent(_configPanelRoot.transform, false);
            var contentRect = contentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            var contentLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.spacing = 12f;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(20, 20, 20, 20);
            
            // Title
            var titleObj = CreateTextObject(contentRoot.transform, "Title", $"Configure: {_currentConfigModName}", 1.2f, TextAnchor.MiddleCenter, out _, out _);
            var titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 40f;
            
            // Scrollable list of settings
            var scrollRoot = new GameObject("ScrollRoot");
            scrollRoot.transform.SetParent(contentRoot.transform, false);
            var scrollRootRect = scrollRoot.AddComponent<RectTransform>();
            scrollRootRect.anchorMin = Vector2.zero;
            scrollRootRect.anchorMax = Vector2.one;
            scrollRootRect.sizeDelta = new Vector2(panelWidth - 40f, panelHeight - 180f);
            var scrollLayout = scrollRoot.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.preferredHeight = panelHeight - 180f;
            scrollLayout.preferredWidth = panelWidth - 40f;
            
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            scrollRect.viewport = viewportRect;
            
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentListRect = content.AddComponent<RectTransform>();
            contentListRect.anchorMin = new Vector2(0f, 1f);
            contentListRect.anchorMax = new Vector2(1f, 1f);
            contentListRect.pivot = new Vector2(0.5f, 1f);
            contentListRect.anchoredPosition = Vector2.zero;
            contentListRect.sizeDelta = new Vector2(0f, 0f);
            _configListContent = contentListRect;
            scrollRect.content = contentListRect;
            
            var listLayout = content.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 10f;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandHeight = false;
            listLayout.padding = new RectOffset(10, 10, 10, 10);
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            
            Debug.Log($"[ModManager] Building config controls for {_currentConfigEntries.Count} entries");
            
            // Create controls for each config entry
            foreach (var entry in _currentConfigEntries)
            {
                CreateConfigControl(content.transform, entry);
                Debug.Log($"[ModManager] Created control for: {entry.DisplayName}");
            }
            
            // Buttons - use vertical layout with individual button rows
            var saveRow = CreateButtonRow(content.transform, "--- SAVE ---", () => OnConfigSavePressed(), true);
            SetButtonLabelColor(saveRow, Color.green);
            
            var cancelRow = CreateButtonRow(content.transform, "--- CANCEL ---", () => OnConfigCancelPressed(), true);
            SetButtonLabelColor(cancelRow, new Color(1f, 0.5f, 0.5f));
        }
        
        private void CreateConfigControl(Transform parent, ModConfigEntry entry)
        {
            // For Bool type, create a simple toggle button with label
            if (entry.Type == ConfigType.Bool)
            {
                bool isOn = entry.GetBool();
                string btnLabel = $"{entry.DisplayName}: {(isOn ? "ON" : "OFF")}";
                
                var toggleRow = CreateButtonRow(parent, btnLabel, null, true);
                SetButtonLabelColor(toggleRow, isOn ? Color.green : new Color(1f, 0.5f, 0.5f));
                SetButtonLabelScale(toggleRow, 0.8f);
                
                // Store for toggling
                ClearButtonHandlers(toggleRow);
                var button = toggleRow.GetComponent<TouchButton>();
                if (button != null)
                {
                    InitializeButton(button);
                    ModConfigEntry capturedEntry = entry;
                    GameObject capturedRow = toggleRow;
                    button.AddOnClickedEvent(() =>
                    {
                        bool newVal = !capturedEntry.GetBool();
                        capturedEntry.CurrentValue = newVal;
                        string newLabel = $"{capturedEntry.DisplayName}: {(newVal ? "ON" : "OFF")}";
                        SetButtonLabel(capturedRow, newLabel);
                        SetButtonLabelColor(capturedRow, newVal ? Color.green : new Color(1f, 0.5f, 0.5f));
                    });
                }
                
                _configInputFields[entry.Key] = toggleRow;
            }
            else
            {
                // For other types, show as info row (read-only for now)
                string valueStr = entry.Serialize();
                var infoRow = CreateButtonRow(parent, $"{entry.DisplayName}: {valueStr}", null, false);
                SetButtonLabelScale(infoRow, 0.8f);
                _configInputFields[entry.Key] = infoRow;
            }
        }
        
        private void SetConfigPanelVisible(bool visible)
        {
            if (_configPanelCanvasGroup == null) return;
            _configPanelCanvasGroup.alpha = visible ? 1f : 0f;
            _configPanelCanvasGroup.interactable = visible;
            _configPanelCanvasGroup.blocksRaycasts = visible;
        }
        
        private void OnConfigSavePressed()
        {
            Debug.Log($"[ModManager] OnConfigSavePressed called for {_currentConfigModName}");
            Debug.Log($"[ModManager] Entry count: {_currentConfigEntries?.Count ?? 0}");
            
            // Save all config values
            var settings = new Dictionary<string, string>();
            foreach (var entry in _currentConfigEntries)
            {
                string serialized = entry.Serialize();
                settings[entry.Key] = serialized;
                Debug.Log($"[ModManager] Saving {entry.Key} = {serialized} (CurrentValue: {entry.CurrentValue})");
                
                // Notify the mod via reflection or direct call
                if (_currentConfigMod != null)
                {
                    _currentConfigMod.OnConfigChanged(entry.Key, entry.CurrentValue);
                }
                else if (_reflectionConfigMod != null && _onConfigChangedMethod != null)
                {
                    try
                    {
                        _onConfigChangedMethod.Invoke(_reflectionConfigMod, new object[] { entry.Key, entry.CurrentValue });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ModManager] Failed to call OnConfigChanged: {ex.Message}");
                    }
                }
            }
            _config.SetModSettings(_currentConfigModName, settings);
            _config.Save();
            
            Debug.Log($"[ModManager] Saved {settings.Count} settings for {_currentConfigModName}");
            SetConfigPanelVisible(false);
        }
        
        private void OnConfigResetPressed()
        {
            // Reset to defaults
            foreach (var entry in _currentConfigEntries)
            {
                entry.CurrentValue = entry.DefaultValue;
            }
            // Rebuild the panel
            BuildConfigPanel();
            SetConfigPanelVisible(true);
        }
        
        private void OnConfigCancelPressed()
        {
            SetConfigPanelVisible(false);
        }

        private void CreateFooterButton(Transform parent, string label, Action onClick)
        {
            var buttonObject = Instantiate(_buttonTemplate, parent);
            buttonObject.name = $"Button_{label.Replace(" ", string.Empty)}";
            SetButtonLabel(buttonObject, label);
            SetButtonLabelScale(buttonObject, 0.95f);
            ClearButtonHandlers(buttonObject);
            var button = buttonObject.GetComponent<TouchButton>();
            if (button != null)
            {
                InitializeButton(button);
                button.AddOnClickedEvent(() => onClick());
            }
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = GetTemplateHeight();
        }

        private GameObject CreateButtonRow(Transform parent, string label, Action onClick, bool interactable)
        {
            var buttonObject = Instantiate(_buttonTemplate, parent);
            buttonObject.name = "ModRow";
            SetButtonLabel(buttonObject, label);
            SetButtonLabelScale(buttonObject, 0.85f);
            ClearButtonHandlers(buttonObject);
            var button = buttonObject.GetComponent<TouchButton>();
            if (button != null)
            {
                InitializeButton(button);
                button.interactable = interactable;
                button.AddOnClickedEvent(() => onClick());
            }
            var layout = buttonObject.GetComponent<LayoutElement>();
            if (layout == null) layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = Mathf.Max(65f, GetTemplateHeight() * 0.9f);
            layout.minHeight = 60f;
            return buttonObject;
        }

        private void InitializeButton(TouchButton button)
        {
            if (button == null || button.IsInitialized)
            {
                return;
            }

            if (_appScope == null && _mainMenu != null)
            {
                _appScope = GetAppScope(_mainMenu);
            }

            if (_appScope == null)
            {
                return;
            }

            try
            {
                button.Initialize(_appScope);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModManager] Failed to initialize button: {ex.Message}");
            }
        }

        private void ClearButtonHandlers(GameObject buttonObject)
        {
            var button = buttonObject.GetComponent<TouchButton>();
            if (button == null)
            {
                return;
            }

            var onClickField = typeof(TouchButton).GetField("_onClick",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (onClickField != null)
            {
                onClickField.SetValue(button, new TouchButton.ButtonClickedEvent());
            }
        }

        private void SetButtonLabel(GameObject buttonObject, string label)
        {
            var localizedText = buttonObject.GetComponentInChildren<LocalizedTextUI>(true);
            if (localizedText != null)
            {
                localizedText.ignoreLocalization = true;
                if (localizedText.TextField != null)
                {
                    localizedText.TextField.text = label;
                    return;
                }
            }

            var tmpText = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = label;
                return;
            }

            var uiText = buttonObject.GetComponentInChildren<Text>(true);
            if (uiText != null)
            {
                uiText.text = label;
            }
        }

        private void SetButtonLabelScale(GameObject buttonObject, float scale)
        {
            var localizedText = buttonObject.GetComponentInChildren<LocalizedTextUI>(true);
            if (localizedText != null && localizedText.TextField != null)
            {
                localizedText.TextField.fontSize = Mathf.RoundToInt(localizedText.TextField.fontSize * scale);
                return;
            }

            var tmpText = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.fontSize = Mathf.RoundToInt(tmpText.fontSize * scale);
                return;
            }

            var uiText = buttonObject.GetComponentInChildren<Text>(true);
            if (uiText != null)
            {
                uiText.fontSize = Mathf.RoundToInt(uiText.fontSize * scale);
            }
        }

        private void SetButtonLabelColor(GameObject buttonObject, Color color)
        {
            var localizedText = buttonObject.GetComponentInChildren<LocalizedTextUI>(true);
            if (localizedText != null && localizedText.TextField != null)
            {
                localizedText.TextField.color = color;
                return;
            }

            var tmpText = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.color = color;
                return;
            }

            var uiText = buttonObject.GetComponentInChildren<Text>(true);
            if (uiText != null)
            {
                uiText.color = color;
            }
        }

        private GameObject CreateTextObject(Transform parent, string name, string text, float scale, TextAnchor alignment, out TMP_Text tmpText, out Text legacyText)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 40f);

            tmpText = null;
            legacyText = null;

            if (_textTemplate != null)
            {
                var tmpUi = textObject.AddComponent<TextMeshProUGUI>();
                tmpUi.font = _textTemplate.font;
                tmpUi.fontSize = Mathf.RoundToInt(_textTemplate.fontSize * scale);
                tmpUi.enableAutoSizing = true;
                tmpUi.fontSizeMin = Mathf.Max(12f, _textTemplate.fontSize * scale * 0.6f);
                tmpUi.fontSizeMax = _textTemplate.fontSize * scale;
                tmpUi.fontStyle = _textTemplate.fontStyle;
                tmpUi.alignment = ConvertAlignment(alignment);
                tmpUi.color = _textTemplate.color;
                tmpUi.material = _textTemplate.material;
                tmpUi.enableWordWrapping = false;
                tmpUi.overflowMode = TextOverflowModes.Ellipsis;
                tmpUi.text = text;
                tmpUi.raycastTarget = false;
                tmpText = tmpUi;
                return textObject;
            }

            if (_legacyTextTemplate != null)
            {
                legacyText = textObject.AddComponent<Text>();
                legacyText.font = _legacyTextTemplate.font;
                legacyText.fontSize = Mathf.RoundToInt(_legacyTextTemplate.fontSize * scale);
                legacyText.fontStyle = _legacyTextTemplate.fontStyle;
                legacyText.alignment = alignment;
                legacyText.color = _legacyTextTemplate.color;
                legacyText.text = text;
                legacyText.raycastTarget = false;
                return textObject;
            }

            legacyText = textObject.AddComponent<Text>();
            legacyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            legacyText.fontSize = Mathf.RoundToInt(24f * scale);
            legacyText.alignment = alignment;
            legacyText.color = Color.white;
            legacyText.text = text;
            legacyText.raycastTarget = false;
            return textObject;
        }

        private void CreateInfoLabel(Transform parent, string text)
        {
            var labelObject = CreateTextObject(parent, "Info", text, 0.9f, TextAnchor.UpperCenter, out var _, out var _);
            var layout = labelObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 26f;
        }

        private void SetLabelText(TMP_Text tmpText, Text legacyText, string text)
        {
            if (tmpText != null)
            {
                tmpText.text = text;
                return;
            }

            if (legacyText != null)
            {
                legacyText.text = text;
            }
        }

        private void ApplyFileChanges()
        {
            var allMods = GetAllModPathsSafe();
            foreach (var modPath in allMods)
            {
                if (!_modStates.TryGetValue(modPath, out var enabled))
                {
                    continue;
                }

                if (IsModManagerSelf(modPath))
                {
                    continue;
                }

                if (ApplyFileState(modPath, enabled))
                {
                    _config.SetModEnabled(modPath, enabled);
                }
            }
        }

        private bool ApplyFileState(string logicalPath, bool enabled)
        {
            if (string.IsNullOrEmpty(logicalPath))
            {
                return false;
            }

            if (!_logicalToActualPath.TryGetValue(logicalPath, out var actualPath))
            {
                actualPath = logicalPath;
                if (!File.Exists(actualPath))
                {
                    var disabledCandidate = logicalPath + DisabledSuffix;
                    if (File.Exists(disabledCandidate))
                    {
                        actualPath = disabledCandidate;
                    }
                    else
                    {
                        Debug.LogWarning($"[ModManager] Could not find mod file to update: {logicalPath}");
                        return false;
                    }
                }
            }

            bool isDisabledFile = actualPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            if (enabled)
            {
                if (!isDisabledFile)
                {
                    return true;
                }

                var target = logicalPath;
                return MoveFile(actualPath, target);
            }

            if (isDisabledFile)
            {
                return true;
            }

            var disabledTarget = logicalPath + DisabledSuffix;
            return MoveFile(actualPath, disabledTarget);
        }

        private bool MoveFile(string source, string target)
        {
            try
            {
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(source, target);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModManager] Failed to move {source} -> {target}: {ex.Message}");
                return false;
            }
        }

        private bool IsModManagerSelf(string modPath)
        {
            if (string.IsNullOrEmpty(modPath))
            {
                return false;
            }

            var fileName = Path.GetFileNameWithoutExtension(modPath);
            return string.Equals(fileName, "ModManager", StringComparison.OrdinalIgnoreCase);
        }

        private Image FindBestBackgroundImage(Transform root)
        {
            Image best = null;
            float bestScore = float.MinValue;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                var rect = image.rectTransform;
                if (!IsFullScreenRect(rect) && !IsNearlyFullScreen(rect))
                {
                    continue;
                }

                float area = Mathf.Abs(rect.rect.width * rect.rect.height);
                float score = area;

                string name = image.gameObject.name.ToLowerInvariant();
                string spriteName = image.sprite != null ? image.sprite.name.ToLowerInvariant() : string.Empty;

                if (name.Contains("background") || name.Contains("backdrop") || name.Contains("bg"))
                {
                    score += 5000f;
                }

                if (spriteName.Contains("background") || spriteName.Contains("backdrop") || spriteName.Contains("bg"))
                {
                    score += 4000f;
                }

                if (IsFullScreenRect(rect))
                {
                    score += 3000f;
                }

                if (name.Contains("profile") || name.Contains("avatar") || name.Contains("icon") || name.Contains("button") || name.Contains("badge") || name.Contains("ring"))
                {
                    score -= 2500f;
                }

                if (rect.rect.width < 300f || rect.rect.height < 300f)
                {
                    score -= 2000f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = image;
                }
            }

            return best;
        }

        private bool IsNearlyFullScreen(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            float width = Mathf.Abs(rect.rect.width);
            float height = Mathf.Abs(rect.rect.height);
            bool largeRect = width >= ReferenceWidth * 0.85f && height >= ReferenceHeight * 0.85f;
            bool anchorsStretch = rect.anchorMin.x <= 0.1f && rect.anchorMax.x >= 0.9f
                && rect.anchorMin.y <= 0.1f && rect.anchorMax.y >= 0.9f;
            return largeRect || anchorsStretch;
        }

        private bool IsFullScreenRect(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            bool anchorsStretch = Mathf.Abs(rect.anchorMin.x) < 0.01f
                && Mathf.Abs(rect.anchorMin.y) < 0.01f
                && Mathf.Abs(rect.anchorMax.x - 1f) < 0.01f
                && Mathf.Abs(rect.anchorMax.y - 1f) < 0.01f;

            bool offsetsNearZero = rect.offsetMin.sqrMagnitude < 1f && rect.offsetMax.sqrMagnitude < 1f;
            return anchorsStretch && offsetsNearZero;
        }

        private IScope GetAppScope(MainMenuScreen mainMenu)
        {
            if (mainMenu == null)
            {
                return null;
            }

            var field = typeof(BaseScalingScreen).GetField("_appScope",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var scope = field != null ? field.GetValue(mainMenu) as IScope : null;
            if (scope != null)
            {
                return scope;
            }

            var screenStackField = typeof(BaseScalingScreen).GetField("_screenStack",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var screenStack = screenStackField != null ? screenStackField.GetValue(mainMenu) : null;
            if (screenStack == null)
            {
                return null;
            }

            var stackScopeField = screenStack.GetType().GetField("_appScope",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return stackScopeField != null ? stackScopeField.GetValue(screenStack) as IScope : null;
        }

        private float GetTemplateHeight()
        {
            if (_buttonTemplate == null)
            {
                return 60f;
            }

            var rect = _buttonTemplate.GetComponent<RectTransform>();
            if (rect != null && rect.sizeDelta.y > 0f)
            {
                return rect.sizeDelta.y;
            }

            return 60f;
        }

        private TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        private void OnDestroy()
        {
            if (_nativeRoot != null)
            {
                Destroy(_nativeRoot);
            }
        }

        private List<string> GetAllModPathsSafe()
        {
            var rawPaths = GetAllModPathsSafeRaw();
            return BuildModIndex(rawPaths);
        }

        private List<string> BuildModIndex(List<string> rawPaths)
        {
            _logicalToActualPath.Clear();
            _disabledOnDisk.Clear();

            var logicalPaths = new List<string>();
            foreach (var rawPath in rawPaths)
            {
                if (string.IsNullOrEmpty(rawPath))
                {
                    continue;
                }

                bool isDisabledFile = rawPath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                string logicalPath = isDisabledFile
                    ? rawPath.Substring(0, rawPath.Length - DisabledSuffix.Length)
                    : rawPath;

                if (!_logicalToActualPath.TryGetValue(logicalPath, out var existing))
                {
                    _logicalToActualPath[logicalPath] = rawPath;
                    logicalPaths.Add(logicalPath);
                }
                else if (!isDisabledFile && existing.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    _logicalToActualPath[logicalPath] = rawPath;
                }

                if (isDisabledFile)
                {
                    _disabledOnDisk.Add(logicalPath);
                }
            }

            return logicalPaths;
        }

        private static List<string> GetAllModPathsSafeRaw()
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendModPathsFromLoader(results, seen);

            var roots = BuildSearchRoots();
            foreach (var root in roots)
            {
                ScanForDlls(root, results, seen);
            }

            return results;
        }

        private static void AppendModPathsFromLoader(List<string> results, HashSet<string> seen)
        {
            try
            {
                var getAllModPaths = typeof(ModLoaderEntry).GetMethod(
                    "GetAllModPaths",
                    BindingFlags.Public | BindingFlags.Static);
                if (getAllModPaths == null)
                {
                    return;
                }

                var value = getAllModPaths.Invoke(null, null);
                if (value is IEnumerable<string> modPaths)
                {
                    foreach (var modPath in modPaths)
                    {
                        TryAddModPath(modPath, results, seen);
                    }
                }
                else if (value != null)
                {
                    Debug.LogWarning("[ModManager] ModLoaderEntry.GetAllModPaths returned unexpected type.");
                }
            }
            catch (TargetInvocationException ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Debug.LogWarning($"[ModManager] ModLoaderEntry.GetAllModPaths failed: {message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModManager] ModLoaderEntry.GetAllModPaths failed: {ex.Message}");
            }
        }

        private static void TryAddModPath(string modPath, List<string> results, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(modPath))
            {
                return;
            }

            var key = NormalizeModPath(modPath);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (seen.Add(key))
            {
                results.Add(modPath);
            }
        }

        private static string NormalizeModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var normalized = path.Trim().Replace('\\', '/');

            if (normalized.Length >= 3 && char.ToUpperInvariant(normalized[0]) == 'Z'
                && normalized[1] == ':' && normalized[2] == '/')
            {
                var unixCandidate = "/" + normalized.Substring(3);
                if (File.Exists(unixCandidate))
                {
                    normalized = unixCandidate;
                }
            }

            try
            {
                normalized = Path.GetFullPath(normalized);
            }
            catch
            {
                return normalized;
            }

            return normalized.Replace('\\', '/');
        }

        private static List<string> BuildSearchRoots()
        {
            var roots = new List<string>();

            AddRoot(roots, Path.Combine(Application.dataPath, "Mods"));
            AddRoot(roots, Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods")));
            return roots;
        }

        private static void AddRoot(List<string> roots, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            for (int i = 0; i < roots.Count; i++)
            {
                if (string.Equals(roots[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            roots.Add(path);
        }

        private static void ScanForDlls(string directory, List<string> results, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                foreach (var file in Directory.GetFiles(directory, "*.dll"))
                {
                    TryAddModPath(file, results, seen);
                }

                foreach (var file in Directory.GetFiles(directory, "*.dll" + DisabledSuffix))
                {
                    TryAddModPath(file, results, seen);
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    ScanForDlls(subDir, results, seen);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ModManager] Error scanning {directory}: {ex.Message}");
            }
        }
    }
}
