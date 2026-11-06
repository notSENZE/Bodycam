using BepInEx.Configuration;
using UnityEngine;

namespace SenzeBodycam;

internal sealed class ModConfig
{
    internal ConfigEntry<bool> ModEnabled { get; }

    internal ConfigEntry<bool> BodyCameraEnabled { get; }
    internal ConfigEntry<KeyboardShortcut> ToggleBodyCameraKey { get; }
    internal ConfigEntry<float> CameraSideways { get; }
    internal ConfigEntry<float> CameraVertical { get; }
    internal ConfigEntry<float> CameraForward { get; }
    internal ConfigEntry<float> AdsOffsetX { get; }
    internal ConfigEntry<float> AdsOffsetY { get; }
    internal ConfigEntry<float> AdsOffsetZ { get; }
    internal ConfigEntry<float> HorizontalFieldOfView { get; }
    internal ConfigEntry<float> CameraWeight { get; }
    internal ConfigEntry<float> MovementIntensity { get; }
    internal ConfigEntry<float> ShotReaction { get; }
    internal ConfigEntry<bool> CornerPeekEnabled { get; }
    internal ConfigEntry<float> CornerPeekStrength { get; }

    internal ConfigEntry<bool> LowLightModeEnabled { get; }
    internal ConfigEntry<KeyboardShortcut> ToggleLowLightModeKey { get; }
    internal ConfigEntry<float> LowLightGain { get; }
    internal ConfigEntry<float> LowLightNoiseStrength { get; }

    internal ConfigEntry<bool> OverlayEnabled { get; }
    internal ConfigEntry<float> SensorNoiseStrength { get; }
    internal ConfigEntry<float> SignalFlickerStrength { get; }
    internal ConfigEntry<bool> FisheyeEffectEnabled { get; }
    internal ConfigEntry<float> FisheyeStrength { get; }
    internal ConfigEntry<float> ChromaticAberrationStrength { get; }
    internal ConfigEntry<float> HighlightBloomStrength { get; }
    internal ConfigEntry<float> MotionBlurStrength { get; }
    internal ConfigEntry<float> CameraExposure { get; }
    internal ConfigEntry<float> EdgeVignetteStrength { get; }
    internal ConfigEntry<bool> RainLensEffectsEnabled { get; }
    internal ConfigEntry<float> LensSmearStrength { get; }
    internal ConfigEntry<float> OverlayScale { get; }
    internal ConfigEntry<float> RecordingLabelOffsetX { get; }
    internal ConfigEntry<float> RecordingLabelOffsetY { get; }
    internal ConfigEntry<float> RecordingLabelScale { get; }
    internal ConfigEntry<float> RecordingTextOffsetX { get; }
    internal ConfigEntry<float> RecordingTextOffsetY { get; }
    internal ConfigEntry<float> RecordingTextScale { get; }
    internal ConfigEntry<float> SenzeLogoOffsetX { get; }
    internal ConfigEntry<float> SenzeLogoOffsetY { get; }
    internal ConfigEntry<float> SenzeLogoScale { get; }
    internal ConfigEntry<float> RecordingOffsetX { get; }
    internal ConfigEntry<float> RecordingOffsetY { get; }
    internal ConfigEntry<float> WirelessOffsetX { get; }
    internal ConfigEntry<float> WirelessOffsetY { get; }
    internal ConfigEntry<float> BatteryOffsetX { get; }
    internal ConfigEntry<float> BatteryOffsetY { get; }
    internal ConfigEntry<float> RaidInfoOffsetX { get; }
    internal ConfigEntry<float> RaidInfoOffsetY { get; }
    internal ConfigEntry<float> StatusHudOffsetX { get; }
    internal ConfigEntry<float> StatusHudOffsetY { get; }
    internal ConfigEntry<float> HeavyBleedingHaloStrength { get; }
    internal ConfigEntry<bool> MagazineCheckLabelEnabled { get; }
    internal ConfigEntry<float> MagazineCheckLabelOffsetX { get; }
    internal ConfigEntry<float> MagazineCheckLabelOffsetY { get; }
    internal ConfigEntry<float> MagazineCheckLabelScale { get; }
    internal ConfigEntry<float> MagazineCheckBackgroundOpacity { get; }
    internal ConfigEntry<bool> FaceCensorEnabled { get; }
    internal ConfigEntry<CensorshipMode> CensorshipStyle { get; }
    internal ConfigEntry<float> FaceCensorSize { get; }
    internal ConfigEntry<float> EyeMosaicWidth { get; }
    internal ConfigEntry<float> EyeMosaicHeight { get; }
    internal ConfigEntry<float> EyeMosaicVerticalOffset { get; }
    internal ConfigEntry<float> CensorshipMaximumDistance { get; }
    internal ConfigEntry<float> CensorshipBlockCount { get; }
    internal ConfigEntry<bool> DamageEffectsEnabled { get; }
    internal ConfigEntry<float> LensDamageStrength { get; }

    internal ModConfig(ConfigFile config)
    {
        ModEnabled = config.Bind(
            "00 - General",
            "Enabled",
            true,
            "Turns the entire mod on or off.");

        BodyCameraEnabled = config.Bind(
            "10 - Bodycam",
            "Enabled",
            true,
            "Turns the bodycam camera on or off. Weapon controls stay unchanged.");
        ToggleBodyCameraKey = config.Bind(
            "10 - Bodycam",
            "Toggle Key",
            new KeyboardShortcut(KeyCode.F10),
            "Press this key to turn the bodycam on or off. The choice is saved.");

        CameraSideways = BindRange(config, "11 - Bodycam Position", "Body Offset X", 0.131f, -0.2f, 0.2f,
            "Moves the physical bodycam mount sideways. Positive = right. Negative = left. This does not move the screen centre.");
        CameraVertical = BindRange(config, "11 - Bodycam Position", "Body Offset Y", 0.085f, -0.15f, 0.3f,
            "Moves the camera up or down. Positive = up. Negative = down.");
        CameraForward = BindRange(config, "11 - Bodycam Position", "Body Offset Z", 0.053f, -0.15f, 0.25f,
            "Moves the camera forward or backward. Positive = forward. Negative = backward.");

        AdsOffsetX = BindRange(config, "11.5 - ADS Position", "ADS Offset X", 0f, -0.2f, 0.2f,
            "Moves the camera sideways only while aiming. Positive = right. Negative = left. Large values can move the sight away from the screen centre.");
        AdsOffsetY = BindRange(config, "11.5 - ADS Position", "ADS Offset Y", 0f, -0.15f, 0.3f,
            "Moves the camera up or down only while aiming. Positive = up. Negative = down. Large values can move the sight away from the screen centre.");
        AdsOffsetZ = BindRange(config, "11.5 - ADS Position", "ADS Offset Z", -0.072f, -0.15f, 0.25f,
            "Moves the camera forward or backward only while aiming. Positive = forward. Negative = backward.");

        HorizontalFieldOfView = BindRange(config, "11.6 - Field of View", "Horizontal FOV", 120f, 120f, 140f,
            "Horizontal bodycam view angle while not aiming. ADS uses the game's normal field of view.");

        CameraWeight = BindRange(config, "12 - Bodycam Motion", "Camera Weight", 0.7f, 0f, 1f,
            "How heavily the camera follows the weapon. Higher = more lag and softer turns. 0 = direct Tarkov turning.");
        MovementIntensity = BindRange(config, "12 - Bodycam Motion", "Movement Intensity", 1.41f, 0f, 2f,
            "Camera movement while walking, running, starting, stopping, and strafing. This also controls the walk and run head bob. 0 = off.");
        ShotReaction = BindRange(config, "12 - Bodycam Motion", "Shot Reaction", 1.6f, 0f, 2f,
            "Camera kick when firing. This does not change weapon recoil. 0 = off.");
        CornerPeekEnabled = config.Bind(
            "12.5 - Experimental Weapon Motion",
            "Corner Peek (Experimental)",
            false,
            "Rotates the weapon toward nearby cover edges while not aiming. This never runs in ADS.");
        CornerPeekStrength = BindRange(config, "12.5 - Experimental Weapon Motion", "Corner Peek Strength (Experimental)", 0.504038f, 0f, 2f,
            "Strength of the automatic weapon yaw and cant near cover. 0 = off. This never affects ADS.");

        LowLightModeEnabled = config.Bind(
            "13 - Low-Light Mode",
            "Enabled",
            false,
            "Brightens dark scenes like a digital bodycam sensor. This is not green night vision.");
        ToggleLowLightModeKey = config.Bind(
            "13 - Low-Light Mode",
            "Toggle Key",
            new KeyboardShortcut(KeyCode.F9),
            "Press this key to turn Low-Light Mode on or off. The choice is saved.");
        LowLightGain = BindRange(config, "13 - Low-Light Mode", "Digital Gain", 2.49f, 0f, 4f,
            "Raises the whole image like a monitor gamma control. Higher values reveal more dark detail and add slightly more grain.");
        LowLightNoiseStrength = BindRange(config, "13 - Low-Light Mode", "Noise Strength", 0.3f, 0f, 1f,
            "Adds moving monochrome sensor grain while Low-Light Mode is active. 0 = no extra grain. 1 = very strong grain.");

        OverlayEnabled = config.Bind(
            "14 - Bodycam Overlay",
            "Enabled",
            true,
            "Shows the Real Bodycam recording overlay.");
        SensorNoiseStrength = BindRange(config, "14 - Bodycam Overlay", "Sensor Noise Strength", 0.25f, 0f, 1f,
            "Amount of fine camera noise. 0 = off.");
        SignalFlickerStrength = BindRange(config, "14 - Bodycam Overlay", "Signal Flicker Strength", 0.5f, 0f, 1f,
            "Amount of irregular brightness flicker. 0 = off.");
        FisheyeEffectEnabled = config.Bind(
            "14.1 - Bodycam Lens",
            "Fisheye Effect",
            true,
            "Turns the wide-angle bodycam lens distortion on or off.");
        FisheyeStrength = BindRange(config, "14.1 - Bodycam Lens", "Fisheye Strength", 24f, 0f, 30f,
            "Strength of the wide-angle barrel distortion. Straight lines bend outward near the screen edges. 0 = off. 30 = very strong.");
        ChromaticAberrationStrength = BindRange(config, "14.1 - Bodycam Lens", "Chromatic Aberration", 0.19f, 0f, 1f,
            "Subtle color separation at the outer lens edge. 0 = off. High values look intentionally stylized.");
        HighlightBloomStrength = BindRange(config, "14.1 - Bodycam Lens", "Highlight Bloom", 0.79f, 0f, 2f,
            "Glow from overexposed lights and reflections. 0 = off. Keep this low for a realistic bodycam image.");
        MotionBlurStrength = BindRange(config, "14.1 - Bodycam Lens", "Motion Blur", 0.08f, 0f, 1f,
            "Small amount of camera smear during fast movement. 0 = off. High values look cinematic rather than realistic.");
        CameraExposure = BindRange(config, "14.1 - Bodycam Lens", "Camera Exposure", 0.44f, -1f, 1f,
            "Camera image brightness. Positive = brighter. Negative = darker. 0 = Tarkov default.");
        EdgeVignetteStrength = BindRange(config, "14.1 - Bodycam Lens", "Edge Vignette", 0.337f, 0f, 0.45f,
            "Darkens only the outer lens edge. The image centre stays clear. 0 = off.");
        RainLensEffectsEnabled = config.Bind(
            "14.2 - Lens Weather",
            "Rain on Lens",
            true,
            "Shows Tarkov's native visor-quality water drops on the camera during rain.");
        LensSmearStrength = BindRange(config, "14.3 - Lens Surface", "Milky Lens Smear Visibility", 0.25f, 0f, 1f,
            "Changes only the visibility of the permanent lens smears. 0% = invisible. 100% = fully visible. No blur or image distortion is added.");
        OverlayScale = BindRange(config, "14 - Bodycam Overlay", "HUD Scale", 0.7230048f, 0.6f, 1.6f,
            "Size of all bodycam HUD elements.");

        RecordingLabelOffsetX = BindRange(config, "14.4 - Recording HUD", "REC Offset X", 51f, -5f, 100f,
            "Moves the REC label sideways. 100 places it at the right edge. Negative = left.");
        RecordingLabelOffsetY = BindRange(config, "14.4 - Recording HUD", "REC Offset Y", 5f, -2f, 100f,
            "Moves the REC label vertically. 100 places it at the bottom edge. Negative = up.");
        RecordingLabelScale = BindRange(config, "14.4 - Recording HUD", "REC Scale", 1f, 0.5f, 2f,
            "Changes the size of the REC label without changing the other HUD elements.");
        RecordingTextOffsetX = BindRange(config, "14.4 - Recording HUD", "Info Text Offset X", -7f, -100f, 5f,
            "Moves the recording text sideways. -100 places the visible text at the left edge. Positive = right.");
        RecordingTextOffsetY = BindRange(config, "14.4 - Recording HUD", "Info Text Offset Y", 1.7f, -2f, 90f,
            "Moves the recording text vertically as a percentage of screen height. Positive = down.");
        RecordingTextScale = BindRange(config, "14.4 - Recording HUD", "Info Text Scale", 1f, 0.5f, 2f,
            "Changes the size of the two recording information lines.");
        SenzeLogoOffsetX = BindRange(config, "14.4 - Recording HUD", "Senze Logo Offset X", -6f, -95f, 5f,
            "Moves the Senze logo sideways as a percentage of screen width. Negative = left. Positive = right.");
        SenzeLogoOffsetY = BindRange(config, "14.4 - Recording HUD", "Senze Logo Offset Y", 1.6f, -1f, 90f,
            "Moves the Senze logo vertically as a percentage of screen height. Positive = down.");
        SenzeLogoScale = BindRange(config, "14.4 - Recording HUD", "Senze Logo Scale", 1f, 0.5f, 2f,
            "Changes the size of the Senze logo without moving the recording text.");

        MagazineCheckLabelEnabled = config.Bind(
            "14.6 - Magazine Check Label",
            "Enabled",
            true,
            "Shows the round count and ammo type beside the magazine during a magazine check.");
        MagazineCheckLabelOffsetX = BindRange(config, "14.6 - Magazine Check Label", "Offset X", -105f, -400f, 400f,
            "Moves the magazine label sideways. Positive = right. Negative = left.");
        MagazineCheckLabelOffsetY = BindRange(config, "14.6 - Magazine Check Label", "Offset Y", -139f, -300f, 300f,
            "Moves the magazine label vertically. Positive = down. Negative = up.");
        MagazineCheckLabelScale = BindRange(config, "14.6 - Magazine Check Label", "Window Scale", 1.6f, 0.6f, 1.8f,
            "Changes the size of the magazine-check window and its text.");
        MagazineCheckBackgroundOpacity = BindRange(config, "14.6 - Magazine Check Label", "Background Opacity", 0.04863853f, 0f, 1f,
            "Changes how dark the magazine-check window is. 0 = transparent. 1 = solid.");

        FaceCensorEnabled = config.Bind(
            "14.7 - Censorship",
            "Enabled",
            true,
            "Covers nearby visible faces with a privacy mosaic during normal raid gameplay. Hidden in inventory, looting and menus. Includes corpses.");
        CensorshipStyle = config.Bind(
            "14.7 - Censorship",
            "Censorship Mode",
            CensorshipMode.EyeMosaicBar,
            "EyeMosaicBar covers only the eyes. FullFaceMosaic keeps the original full-face effect.");
        FaceCensorSize = BindRange(config, "14.7 - Censorship", "Mosaic Size", 1.1f, 0.75f, 1.8f,
            "Changes the overall size of the selected privacy mosaic.");
        EyeMosaicWidth = BindRange(config, "14.7 - Censorship", "Eye Bar Width", 1.1f, 0.7f, 1.8f,
            "Changes the width of the eye mosaic bar. This setting only affects EyeMosaicBar.");
        EyeMosaicHeight = BindRange(config, "14.7 - Censorship", "Eye Bar Height", 1.47f, 0.5f, 1.8f,
            "Changes the height of the eye mosaic bar. This setting only affects EyeMosaicBar.");
        EyeMosaicVerticalOffset = BindRange(config, "14.7 - Censorship", "Eye Bar Vertical Offset", -0.016f, -0.08f, 0.08f,
            "Moves the eye mosaic bar up or down on the head. Positive = up. Negative = down.");
        CensorshipMaximumDistance = BindRange(config, "14.7 - Censorship", "Maximum Distance", 80f, 10f, 120f,
            "Farthest distance in metres at which visible faces are censored.");
        CensorshipBlockCount = BindRange(config, "14.7 - Censorship", "Mosaic Blocks", 10f, 5f, 18f,
            "Pixel blocks across the mosaic at close range. Lower = larger blocks. The block count automatically decreases with distance.");

        DamageEffectsEnabled = config.Bind(
            "15 - Bodycam Damage Effects",
            "Enabled",
            true,
            "Shows up to two neutral faceshield-style impact marks on the bodycam lens after combat hits.");
        LensDamageStrength = BindRange(config, "15 - Bodycam Damage Effects", "Lens Cracks Visibility", 0.1025821f, 0f, 1f,
            "Changes how visible the two persistent neutral impact marks are. 0 = off.");
    }

    private static ConfigEntry<float> BindRange(
        ConfigFile config,
        string section,
        string name,
        float defaultValue,
        float minimum,
        float maximum,
        string description)
    {
        return config.Bind(
            section,
            name,
            defaultValue,
            new ConfigDescription(description, new AcceptableValueRange<float>(minimum, maximum)));
    }
}
