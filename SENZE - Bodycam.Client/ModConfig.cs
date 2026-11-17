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
    internal ConfigEntry<bool> WideFieldOfViewEnabled { get; }
    internal ConfigEntry<float> HorizontalFieldOfView { get; }
    internal ConfigEntry<float> CameraWeight { get; }
    internal ConfigEntry<float> MovementIntensity { get; }
    internal ConfigEntry<float> ShotReaction { get; }
    internal ConfigEntry<bool> LowLightModeEnabled { get; }
    internal ConfigEntry<KeyboardShortcut> ToggleLowLightModeKey { get; }
    internal ConfigEntry<bool> NightVisionEnabled { get; }
    internal ConfigEntry<KeyboardShortcut> ToggleFlashlightKey { get; }

    internal ConfigEntry<bool> OverlayEnabled { get; }
    internal ConfigEntry<float> SensorNoiseStrength { get; }
    internal ConfigEntry<bool> FisheyeEffectEnabled { get; }
    internal ConfigEntry<float> FisheyeStrength { get; }
    internal ConfigEntry<float> FisheyeEdgeCrop { get; }
    internal ConfigEntry<float> ChromaticAberrationStrength { get; }
    internal ConfigEntry<float> MotionBlurStrength { get; }
    internal ConfigEntry<float> EdgeVignetteStrength { get; }
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

        CameraSideways = BindRange(config, "11 - Bodycam Position", "Body Offset X", 0f, -0.2f, 0.2f,
            "Moves the weapon in the image horizontally from its centred default. Negative = left. Positive = right.");
        CameraVertical = BindRange(config, "11 - Bodycam Position", "Body Offset Y", 0.1188028f, -0.15f, 0.3f,
            "Moves the camera up or down. Positive = up. Negative = down.");
        CameraForward = BindRange(config, "11 - Bodycam Position", "Body Offset Z", 0.1175305f, -0.15f, 0.25f,
            "Moves the camera forward or backward. Positive = forward. Negative = backward.");

        AdsOffsetX = BindRange(config, "11.5 - ADS Position", "ADS Offset X", 0f, -0.2f, 0.2f,
            "Moves the camera sideways only while aiming. Positive = right. Negative = left. Large values can move the sight away from the screen centre.");
        AdsOffsetY = BindRange(config, "11.5 - ADS Position", "ADS Offset Y", 0f, -0.15f, 0.3f,
            "Moves the camera up or down only while aiming. Positive = up. Negative = down. Large values can move the sight away from the screen centre.");
        AdsOffsetZ = BindRange(config, "11.5 - ADS Position", "ADS Offset Z", 0.032f, -0.15f, 0.25f,
            "Moves the camera forward or backward only while aiming. Positive = forward. Negative = backward.");

        WideFieldOfViewEnabled = config.Bind(
            "11.6 - Field of View",
            "Enabled",
            true,
            "Applies the wide bodycam view. The same angle stays active while aiming.");
        HorizontalFieldOfView = BindRange(config, "11.6 - Field of View", "Horizontal FOV", 120f, 120f, 145f,
            "Horizontal view angle in degrees. Changes take effect immediately.");

        CameraWeight = BindRange(config, "12 - Bodycam Motion", "Camera Weight", 0.8985916f, 0f, 1f,
            "How heavily the camera follows the weapon. Higher = more lag and softer turns. 0 = direct Tarkov turning.");
        MovementIntensity = BindRange(config, "12 - Bodycam Motion", "Movement Intensity", 1.56f, 0f, 2f,
            "Camera movement while walking, running, starting, stopping, and strafing. This also controls the walk and run head bob. 0 = off.");
        ShotReaction = BindRange(config, "12 - Bodycam Motion", "Shot Reaction", 0.71f, 0f, 2f,
            "Camera kick when firing. This does not change weapon recoil. 0 = off.");

        LowLightModeEnabled = config.Bind(
            "13 - Low-Light Mode",
            "Enabled",
            false,
            "Brightens dark scenes like a digital bodycam sensor. F9 cycles Off, Low-Light and Night Vision.");
        ToggleLowLightModeKey = config.Bind(
            "13 - Low-Light Mode",
            "Toggle Key",
            new KeyboardShortcut(KeyCode.F9),
            "Cycles Off, Low-Light and Night Vision. The choice is saved.");
        NightVisionEnabled = config.Bind(
            "13.5 - Night Vision",
            "Enabled",
            false,
            "Shows a green, grainy night-vision image. F9 cycles the vision modes.");
        ToggleFlashlightKey = config.Bind(
            "13.6 - Bodycam Flashlight",
            "Toggle Key",
            new KeyboardShortcut(KeyCode.H),
            "Turns the weak, wide bodycam flashlight on or off during a raid.");

        OverlayEnabled = config.Bind(
            "14 - Bodycam Overlay",
            "Enabled",
            true,
            "Shows the REC label, operator information and Senze logo.");
        SensorNoiseStrength = BindRange(config, "14 - Bodycam Overlay", "Sensor Noise Strength", 0.25f, 0f, 1f,
            "Amount of fine camera noise. 0 = off.");
        FisheyeEffectEnabled = config.Bind(
            "14.1 - Bodycam Lens",
            "Fisheye Effect",
            true,
            "Turns the wide-angle bodycam lens distortion on or off.");
        FisheyeStrength = BindRange(config, "14.1 - Bodycam Lens", "Fisheye Strength", 24.22535f, 0f, 30f,
            "Strength of the barrel-shaped wide-angle curve near the screen edges. 0 = off. 30 = very strong bodycam distortion.");
        FisheyeEdgeCrop = BindRange(config, "14.1 - Bodycam Lens", "Fisheye Edge Crop", 1.076385f, 1f, 1.25f,
            "Slightly zooms the image to hide stretched outer edges.");
        ChromaticAberrationStrength = BindRange(config, "14.1 - Bodycam Lens", "Chromatic Aberration", 0.2f, 0f, 1f,
            "Amount of color separation near the screen edges. 0 = off.");
        MotionBlurStrength = BindRange(config, "14.1 - Bodycam Lens", "Motion Blur", 0.1544601f, 0f, 1f,
            "Blur during fast camera movement. 0 = off.");
        EdgeVignetteStrength = BindRange(config, "14.1 - Bodycam Lens", "Edge Vignette", 0.29f, 0f, 0.45f,
            "Adds a rounded black lens edge without darkening the image centre. 0 = off.");
        LensSmearStrength = BindRange(config, "14.3 - Lens Surface", "Milky Lens Smear Visibility", 0.1028169f, 0f, 1f,
            "Changes only the visibility of the permanent lens smears. 0% = invisible. 100% = fully visible. No blur or image distortion is added.");
        OverlayScale = BindRange(config, "14 - Bodycam Overlay", "HUD Scale", 0.7230048f, 0.6f, 1.6f,
            "Size of all bodycam HUD elements.");

        RecordingLabelOffsetX = BindRange(config, "14.4 - Recording HUD", "REC Offset X", 51f, -5f, 100f,
            "Moves the REC label sideways. 100 places it at the right edge.");
        RecordingLabelOffsetY = BindRange(config, "14.4 - Recording HUD", "REC Offset Y", 5f, -2f, 100f,
            "Moves the REC label vertically. 100 places it at the bottom edge.");
        RecordingLabelScale = BindRange(config, "14.4 - Recording HUD", "REC Scale", 1f, 0.5f, 2f,
            "Changes the size of the REC label.");
        RecordingTextOffsetX = BindRange(config, "14.4 - Recording HUD", "Info Text Offset X", -7f, -100f, 5f,
            "Moves the recording text sideways. -100 places it at the left edge.");
        RecordingTextOffsetY = BindRange(config, "14.4 - Recording HUD", "Info Text Offset Y", 1.7f, -2f, 90f,
            "Moves the recording text vertically. Positive = down.");
        RecordingTextScale = BindRange(config, "14.4 - Recording HUD", "Info Text Scale", 1f, 0.5f, 2f,
            "Changes the size of the recording information.");
        SenzeLogoOffsetX = BindRange(config, "14.4 - Recording HUD", "Senze Logo Offset X", -5.061033f, -95f, 5f,
            "Moves the Senze logo sideways. Negative = left.");
        SenzeLogoOffsetY = BindRange(config, "14.4 - Recording HUD", "Senze Logo Offset Y", 1.6f, -1f, 90f,
            "Moves the Senze logo vertically. Positive = down.");
        SenzeLogoScale = BindRange(config, "14.4 - Recording HUD", "Senze Logo Scale", 1f, 0.5f, 2f,
            "Changes the size of the Senze logo.");

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
