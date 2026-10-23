using BepInEx;
using HarmonyLib;

namespace SenzeBodycam;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.SPT.core", "4.1.6")]
[BepInProcess("EscapeFromTarkov.exe")]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginGuid = "com.senze.bodycam";
    internal const string PluginName = "Senze - Bodycam";
    internal const string PluginVersion = "1.0.0";

    private Harmony _harmony;
    private ModConfig _settings;
    private BodyCameraController _bodyCamera;
    private BodycamOverlay _bodycamOverlay;

    private void Awake()
    {
        _settings = new ModConfig(Config);
        _bodyCamera = new BodyCameraController(_settings, Logger);
        _bodycamOverlay = new BodycamOverlay(_settings, _bodyCamera);

        PatchContext.BodyCamera = _bodyCamera;
        PatchContext.BodycamOverlay = _bodycamOverlay;

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for SPT 4.1.6.");
    }

    private void Update()
    {
        _bodyCamera?.UpdateInput();
    }

    private void OnGUI()
    {
        _bodycamOverlay?.Draw();
    }

    private void OnDestroy()
    {
        PatchContext.BodyCamera = null;
        PatchContext.BodycamOverlay = null;
        _bodycamOverlay?.Dispose();
        _bodyCamera?.Reset();
        _harmony?.UnpatchSelf();
    }
}
