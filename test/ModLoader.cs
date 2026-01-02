using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace MiniMotorwaysModLoader
{
    /// <summary>
    /// Mini Motorways Mod Loader v1.1.0
    /// Developed by Roofus
    /// </summary>
    public static class ModLoaderEntry
    {
        private static readonly List<IMod> LoadedMods = new List<IMod>();
        private static ModLoaderSplash _splashScreen;
        public static bool ModsLoaded { get; private set; } = false;
        
        public static void Main()
        {
            try
            {
                Debug.Log("========================================");
                Debug.Log("[MODLOADER] Mini Motorways Mod Loader v1.1.0");
                Debug.Log("[MODLOADER] Developed by Roofus");
                Debug.Log("[MODLOADER] Loaded at: " + DateTime.Now.ToString());
                Debug.Log("========================================");
                
                CreateSplashScreen();
                InitializeModLoader();
                ModsLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[MODLOADER] Fatal error: " + ex.Message);
                Debug.LogError(ex.StackTrace);
                ModsLoaded = true;
            }
        }
        
        private static void CreateSplashScreen()
        {
            try
            {
                var splashGO = new GameObject("ModLoaderSplash");
                _splashScreen = splashGO.AddComponent<ModLoaderSplash>();
                UnityEngine.Object.DontDestroyOnLoad(splashGO);
            }
            catch { }
        }
        
        private static void InitializeModLoader()
        {
            string modsPath = Path.Combine(Application.dataPath, "Mods");
            
            if (!Directory.Exists(modsPath))
            {
                Debug.Log("[MODLOADER] Creating Mods directory at: " + modsPath);
                Directory.CreateDirectory(modsPath);
                _splashScreen?.SetStatus("No mods found - Mods folder created");
                return;
            }
            
            Debug.Log("[MODLOADER] Scanning for mods in: " + modsPath);
            _splashScreen?.SetStatus("Scanning for mods...");
            
            var modDlls = new List<string>();
            ScanForDlls(modsPath, modDlls);
            
            Debug.Log("[MODLOADER] Found " + modDlls.Count + " DLL file(s)");
            
            if (modDlls.Count == 0)
            {
                Debug.Log("[MODLOADER] No mods found.");
                _splashScreen?.SetStatus("No mods found");
                return;
            }
            
            _splashScreen?.SetModCount(modDlls.Count);
            
            int loadedCount = 0;
            foreach (var dllPath in modDlls)
            {
                string modName = Path.GetFileNameWithoutExtension(dllPath);
                _splashScreen?.SetCurrentMod(modName, loadedCount + 1, modDlls.Count);
                LoadMod(dllPath);
                loadedCount++;
            }
            
            Debug.Log("[MODLOADER] Successfully loaded " + LoadedMods.Count + " mod(s)");
            _splashScreen?.SetComplete(LoadedMods.Count);
        }
        
        private static void ScanForDlls(string directory, List<string> results)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory, "*.dll"))
                    results.Add(file);
                
                foreach (var subDir in Directory.GetDirectories(directory))
                    ScanForDlls(subDir, results);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MODLOADER] Error scanning " + directory + ": " + ex.Message);
            }
        }
        
        private static void LoadMod(string dllPath)
        {
            try
            {
                Debug.Log("[MODLOADER] Loading: " + Path.GetFileName(dllPath));
                
                Assembly modAssembly = Assembly.LoadFrom(dllPath);
                
                var modTypes = modAssembly.GetTypes()
                    .Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                
                foreach (var modType in modTypes)
                {
                    try
                    {
                        var mod = (IMod)Activator.CreateInstance(modType);
                        
                        Debug.Log("[MODLOADER]   -> " + mod.Name + " v" + mod.Version + " by " + mod.Author);
                        
                        _splashScreen?.AddLoadedMod(mod.Name, mod.Version);
                        
                        mod.OnLoad();
                        LoadedMods.Add(mod);
                        
                        Debug.Log("[MODLOADER]   -> OK");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("[MODLOADER]   -> Failed: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[MODLOADER] Failed to load " + Path.GetFileName(dllPath) + ": " + ex.Message);
            }
        }
        
        public static IReadOnlyList<IMod> GetLoadedMods() => LoadedMods.AsReadOnly();
        
        public static List<string> GetAllModPaths()
        {
            string modsPath = Path.Combine(Application.dataPath, "Mods");
            if (!Directory.Exists(modsPath)) return new List<string>();
            
            var modDlls = new List<string>();
            ScanForDlls(modsPath, modDlls);
            return modDlls;
        }
        
        public static void Shutdown()
        {
            Debug.Log("[MODLOADER] Shutting down mods...");
            foreach (var mod in LoadedMods)
            {
                try
                {
                    mod.OnUnload();
                    Debug.Log("[MODLOADER] " + mod.Name + " unloaded");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[MODLOADER] Error unloading " + mod.Name + ": " + ex.Message);
                }
            }
            LoadedMods.Clear();
        }
    }
    
    public class ModLoaderSplash : MonoBehaviour
    {
        private string _status = "Initializing...";
        private int _currentModIndex = 0;
        private int _totalMods = 0;
        private List<string> _loadedMods = new List<string>();
        private float _displayTime = 0f;
        private bool _complete = false;
        private float _fadeOutTime = 0f;
        private const float FADE_DURATION = 0.8f;
        
        private Texture2D _blackTex;
        private Texture2D _panelTex;
        private Texture2D _borderTex;
        private Texture2D _progressTex;
        private Texture2D _progressBgTex;
        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _modListStyle;
        private bool _stylesInit = false;
        private bool _startFadeOut = false;
        private float _minDisplayTime = 3.0f; // Time splash stays visible AFTER loading completes
        private float _timeSinceComplete = 0f; // Tracks time after SetComplete is called
        
        private void Update()
        {
            _displayTime += Time.unscaledDeltaTime;
            
            // Only track time after loading is complete
            if (_complete)
            {
                _timeSinceComplete += Time.unscaledDeltaTime;
                
                // Start fade out after minimum display time since completion
                if (_timeSinceComplete >= _minDisplayTime && !_startFadeOut)
                {
                    _startFadeOut = true;
                }
            }
            
            if (_startFadeOut)
            {
                _fadeOutTime += Time.unscaledDeltaTime;
                if (_fadeOutTime > FADE_DURATION)
                    Destroy(gameObject);
            }
        }
        
        public void SetStatus(string status) => _status = status;
        public void SetModCount(int count) { _totalMods = count; _status = "Found " + count + " mod(s)"; }
        public void SetCurrentMod(string modName, int index, int total) { _currentModIndex = index; _totalMods = total; _status = "Loading " + index + "/" + total + "..."; }
        public void AddLoadedMod(string name, string version) => _loadedMods.Add(name + " v" + version);
        public void SetComplete(int count) { _complete = true; _timeSinceComplete = 0f; _status = "Loaded " + count + " mod(s)!"; }
        
        private void InitStyles()
        {
            if (_stylesInit) return;
            
            _blackTex = MakeTex(new Color(0.05f, 0.05f, 0.08f, 1f));
            _panelTex = MakeTex(new Color(0.12f, 0.12f, 0.18f, 1f));
            _borderTex = MakeTex(new Color(0.9f, 0.7f, 0.3f, 1f));
            _progressTex = MakeTex(new Color(0.3f, 0.7f, 0.3f, 1f));
            _progressBgTex = MakeTex(new Color(0.2f, 0.2f, 0.25f, 1f));
            
            _titleStyle = new GUIStyle();
            _titleStyle.fontSize = 28;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.normal.textColor = new Color(0.9f, 0.7f, 0.3f);
            
            _statusStyle = new GUIStyle();
            _statusStyle.fontSize = 16;
            _statusStyle.alignment = TextAnchor.MiddleCenter;
            _statusStyle.normal.textColor = Color.white;
            
            _modListStyle = new GUIStyle();
            _modListStyle.fontSize = 14;
            _modListStyle.alignment = TextAnchor.UpperLeft;
            _modListStyle.normal.textColor = new Color(0.7f, 0.9f, 0.7f);
            
            _stylesInit = true;
        }
        
        private void OnGUI()
        {
            InitStyles();
            
            float alpha = 1f;
            if (_startFadeOut)
            {
                alpha = 1f - (_fadeOutTime / FADE_DURATION);
                alpha = Mathf.Clamp01(alpha);
            }
            
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            
            GUIStyle blackStyle = new GUIStyle();
            blackStyle.normal.background = _blackTex;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, blackStyle);
            
            float width = 500f;
            float height = 300f;
            float x = (Screen.width - width) / 2f;
            float y = (Screen.height - height) / 2f;
            
            GUIStyle panelStyle = new GUIStyle();
            panelStyle.normal.background = _panelTex;
            GUI.Label(new Rect(x, y, width, height), GUIContent.none, panelStyle);
            
            GUIStyle borderStyle = new GUIStyle();
            borderStyle.normal.background = _borderTex;
            GUI.Label(new Rect(x, y, width, 2), GUIContent.none, borderStyle);
            GUI.Label(new Rect(x, y + height - 2, width, 2), GUIContent.none, borderStyle);
            GUI.Label(new Rect(x, y, 2, height), GUIContent.none, borderStyle);
            GUI.Label(new Rect(x + width - 2, y, 2, height), GUIContent.none, borderStyle);
            
            GUI.Label(new Rect(x, y + 20, width, 40), "MINI MOTORWAYS MOD LOADER", _titleStyle);
            
            GUIStyle versionStyle = new GUIStyle(_statusStyle);
            versionStyle.fontSize = 12;
            versionStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(x, y + 55, width, 20), "v1.1.0 by Roofus", versionStyle);
            
            GUI.Label(new Rect(x, y + 90, width, 30), _status, _statusStyle);
            
            if (_totalMods > 0)
            {
                float barWidth = width - 60;
                float barHeight = 20;
                float barX = x + 30;
                float barY = y + 130;
                
                GUIStyle barBgStyle = new GUIStyle();
                barBgStyle.normal.background = _progressBgTex;
                GUI.Label(new Rect(barX, barY, barWidth, barHeight), GUIContent.none, barBgStyle);
                
                float progress = (float)_currentModIndex / _totalMods;
                if (_complete) progress = 1f;
                
                GUIStyle progressStyle = new GUIStyle();
                progressStyle.normal.background = _progressTex;
                GUI.Label(new Rect(barX + 2, barY + 2, (barWidth - 4) * progress, barHeight - 4), GUIContent.none, progressStyle);
            }
            
            if (_loadedMods.Count > 0)
            {
                float listY = y + 170;
                GUI.Label(new Rect(x + 30, listY, width - 60, 20), "Loaded Mods:", _statusStyle);
                
                listY += 25;
                foreach (var mod in _loadedMods)
                {
                    GUI.Label(new Rect(x + 40, listY, width - 80, 18), "• " + mod, _modListStyle);
                    listY += 18;
                    if (listY > y + height - 30) break;
                }
            }
            
            GUI.color = originalColor;
        }
        
        private Texture2D MakeTex(Color col)
        {
            Texture2D result = new Texture2D(2, 2);
            Color[] pix = new Color[4];
            for (int i = 0; i < 4; i++) pix[i] = col;
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
    
    /// <summary>
    /// Base interface for all mods. Implement this to create a mod.
    /// </summary>
    public interface IMod
    {
        string Name { get; }
        string Version { get; }
        string Author { get; }
        void OnLoad();
        void OnUnload();
    }
}
