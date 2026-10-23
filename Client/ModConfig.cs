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
    internal ConfigEntry<float> CameraWeight { get; }
    internal ConfigEntry<float> MovementIntensity { get; }
    internal ConfigEntry<float> ShotReaction { get; }
    internal ConfigEntry<BodycamAdsMode> AdsCameraMode { get; }
    internal ConfigEntry<bool> CornerPeekEnabled { get; }
    internal ConfigEntry<float> CornerPeekStrength { get; }

    internal ConfigEntry<bool> OverlayEnabled { get; }
    internal ConfigEntry<float> SensorNoiseStrength { get; }
    internal ConfigEntry<float> SignalFlickerStrength { get; }
    internal ConfigEntry<bool> FisheyeEffectEnabled { get; }
    internal ConfigEntry<float> FisheyeStrength { get; }
    internal ConfigEntry<float> FisheyeEdgeCrop { get; }
    internal ConfigEntry<float> ChromaticAberrationStrength { get; }
    internal ConfigEntry<float> HighlightBloomStrength { get; }
    internal ConfigEntry<float> NearFocusBlurStrength { get; }
    internal ConfigEntry<float> MotionBlurStrength { get; }
    internal ConfigEntry<float> CameraExposure { get; }
    internal ConfigEntry<float> EdgeVignetteStrength { get; }
    internal ConfigEntry<bool> RainLensEffectsEnabled { get; }
    internal ConfigEntry<float> LensSmearStrength { get; }
    internal ConfigEntry<float> OverlayScale { get; }
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
    internal ConfigEntry<float> FaceCensorSize { get; }
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

        CameraSideways = BindRange(config, "11 - Bodycam Position", "Body Offset X", 0.141784f, -0.2f, 0.2f,
            "Moves the camera sideways. Positive = right. Negative = left.");
        CameraVertical = BindRange(config, "11 - Bodycam Position", "Body Offset Y", 0.085f, -0.15f, 0.3f,
            "Moves the camera up or down. Positive = up. Negative = down.");
        CameraForward = BindRange(config, "11 - Bodycam Position", "Body Offset Z", 0.06307042f, -0.15f, 0.25f,
            "Moves the camera forward or backward. Positive = forward. Negative = backward.");

        CameraWeight = BindRange(config, "12 - Bodycam Motion", "Camera Weight", 1f, 0f, 1f,
            "How heavily the camera follows the weapon. Higher = more lag and softer turns. 0 = direct Tarkov turning.");
        MovementIntensity = BindRange(config, "12 - Bodycam Motion", "Movement Intensity", 1.30216f, 0f, 2f,
            "Camera movement while walking, running, starting, stopping, and strafing. This also controls the walk and run head bob. 0 = off.");
        ShotReaction = BindRange(config, "12 - Bodycam Motion", "Shot Reaction", 0.8f, 0f, 2f,
            "Camera kick when firing. This does not change weapon recoil. 0 = off.");
        AdsCameraMode = config.Bind(
            "12 - Bodycam Motion",
            "ADS Camera Position",
            BodycamAdsMode.Vanilla,
            "Camera position while aiming. Vanilla = Tarkov view. ShoulderAssist = between both views. Bodycam = chest view.");

        CornerPeekEnabled = config.Bind(
            "12.5 - Experimental Weapon Motion",
            "Corner Peek (Experimental)",
            true,
            "Rotates the weapon toward nearby cover edges while not aiming. This never runs in ADS.");
        CornerPeekStrength = BindRange(config, "12.5 - Experimental Weapon Motion", "Corner Peek Strength (Experimental)", 1.30216f, 0f, 2f,
            "Strength of the automatic weapon yaw and cant near cover. 0 = off. This never affects ADS.");

        OverlayEnabled = config.Bind(
            "14 - Bodycam Overlay",
            "Enabled",
            true,
            "Shows the frame, REC label, battery, raid information, and status icons.");
        SensorNoiseStrength = BindRange(config, "14 - Bodycam Overlay", "Sensor Noise Strength", 0.24f, 0f, 1f,
            "Amount of fine camera noise. 0 = off.");
        SignalFlickerStrength = BindRange(config, "14 - Bodycam Overlay", "Signal Flicker Strength", 0.16f, 0f, 1f,
            "Amount of irregular brightness flicker. 0 = off.");
        FisheyeEffectEnabled = config.Bind(
            "14.1 - Bodycam Lens",
            "Fisheye Effect",
            true,
            "Turns the wide-angle bodycam lens distortion on or off.");
        FisheyeStrength = BindRange(config, "14.1 - Bodycam Lens", "Fisheye Strength", 18.21127f, 0f, 30f,
            "Strength of the wide-angle curve near the screen edges. 0 = off.");
        FisheyeEdgeCrop = BindRange(config, "14.1 - Bodycam Lens", "Fisheye Edge Crop", 1.04f, 1f, 1.25f,
            "Slightly zooms the image to hide stretched outer edges.");
        ChromaticAberrationStrength = BindRange(config, "14.1 - Bodycam Lens", "Chromatic Aberration", 0.3020657f, 0f, 1f,
            "Amount of color separation near the screen edges. 0 = off.");
        HighlightBloomStrength = BindRange(config, "14.1 - Bodycam Lens", "Highlight Bloom", 0.7f, 0f, 2f,
            "Soft glow around very bright areas. 0 = off.");
        NearFocusBlurStrength = BindRange(config, "14.1 - Bodycam Lens", "Near Weapon Blur", 0.14f, 0f, 1f,
            "Blurs the nearby weapon slightly when you are not aiming. 0 = off.");
        MotionBlurStrength = BindRange(config, "14.1 - Bodycam Lens", "Motion Blur", 0.3f, 0f, 1f,
            "Blur during fast camera movement. 0 = off.");
        CameraExposure = BindRange(config, "14.1 - Bodycam Lens", "Camera Exposure", 0.08f, -1f, 1f,
            "Camera image brightness. Positive = brighter. Negative = darker. 0 = Tarkov default.");
        EdgeVignetteStrength = BindRange(config, "14.1 - Bodycam Lens", "Edge Vignette", 0.3321127f, 0f, 0.45f,
            "Adds a rounded black lens edge without darkening the image centre. 0 = off.");
        RainLensEffectsEnabled = config.Bind(
            "14.2 - Lens Weather",
            "Rain on Lens",
            true,
            "Shows Tarkov's native visor-quality water drops on the camera during rain.");
        LensSmearStrength = BindRange(config, "14.3 - Lens Surface", "Milky Lens Smear Visibility", 0.18f, 0f, 1f,
            "Changes only the visibility of the permanent lens smears. 0% = invisible. 100% = fully visible. No blur or image distortion is added.");
        OverlayScale = BindRange(config, "14 - Bodycam Overlay", "HUD Scale", 0.7230048f, 0.6f, 1.6f,
            "Size of all bodycam HUD elements.");

        RecordingOffsetX = BindRange(config, "14.5 - HUD Positions", "REC and Operator Offset X", 0f, -800f, 800f,
            "Moves REC and Operator sideways. Positive = right. Negative = left.");
        RecordingOffsetY = BindRange(config, "14.5 - HUD Positions", "REC and Operator Offset Y", 59.15493f, -450f, 450f,
            "Moves REC and Operator vertically. Positive = down. Negative = up.");
        WirelessOffsetX = BindRange(config, "14.5 - HUD Positions", "Wireless Offset X", 0f, -800f, 800f,
            "Moves the wireless icon sideways. Positive = right. Negative = left.");
        WirelessOffsetY = BindRange(config, "14.5 - HUD Positions", "Wireless Offset Y", 0f, -450f, 450f,
            "Moves the wireless icon vertically. Positive = down. Negative = up.");
        BatteryOffsetX = BindRange(config, "14.5 - HUD Positions", "Battery Offset X", 0f, -800f, 800f,
            "Moves the battery icon sideways. Positive = right. Negative = left.");
        BatteryOffsetY = BindRange(config, "14.5 - HUD Positions", "Battery Offset Y", 0f, -450f, 450f,
            "Moves the battery icon vertically. Positive = down. Negative = up.");
        RaidInfoOffsetX = BindRange(config, "14.5 - HUD Positions", "Raid Information Offset X", 22.53521f, -800f, 800f,
            "Moves the date, raid time, and location sideways. Positive = right. Negative = left.");
        RaidInfoOffsetY = BindRange(config, "14.5 - HUD Positions", "Raid Information Offset Y", -12.67606f, -450f, 450f,
            "Moves the date, raid time, and location vertically. Positive = down. Negative = up.");
        StatusHudOffsetX = BindRange(config, "14.5 - HUD Positions", "Status HUD Offset X", -30.04695f, -800f, 800f,
            "Moves the health, water, energy, and grenade icons sideways. Positive = right. Negative = left.");
        StatusHudOffsetY = BindRange(config, "14.5 - HUD Positions", "Status HUD Offset Y", 0f, -450f, 450f,
            "Moves the health, water, energy, and grenade icons vertically. Positive = down. Negative = up.");

        HeavyBleedingHaloStrength = BindRange(config, "14.7 - Status HUD", "Heavy Bleeding Halo Strength", 0.85f, 0f, 1.5f,
            "Strength of the red pulse around the heart during heavy bleeding. 0 = off.");

        MagazineCheckLabelEnabled = config.Bind(
            "14.6 - Magazine Check Label",
            "Enabled",
            true,
            "Shows the round count and ammo type beside the magazine during a magazine check.");
        MagazineCheckLabelOffsetX = BindRange(config, "14.6 - Magazine Check Label", "Offset X", 22f, -400f, 400f,
            "Moves the magazine label sideways. Positive = right. Negative = left.");
        MagazineCheckLabelOffsetY = BindRange(config, "14.6 - Magazine Check Label", "Offset Y", -74f, -300f, 300f,
            "Moves the magazine label vertically. Positive = down. Negative = up.");
        MagazineCheckLabelScale = BindRange(config, "14.6 - Magazine Check Label", "Window Scale", 1f, 0.6f, 1.8f,
            "Changes the size of the magazine-check window and its text.");
        MagazineCheckBackgroundOpacity = BindRange(config, "14.6 - Magazine Check Label", "Background Opacity", 0.72f, 0f, 1f,
            "Changes how dark the magazine-check window is. 0 = transparent. 1 = solid.");

        FaceCensorEnabled = config.Bind(
            "14.7 - Censorship",
            "Enabled",
            true,
            "Covers nearby visible faces with a privacy mosaic during normal raid gameplay. Hidden in inventory, looting and menus. Includes corpses.");
        FaceCensorSize = BindRange(config, "14.7 - Censorship", "Mosaic Size", 1.2f, 0.75f, 1.8f,
            "Changes how much of the face the square mosaic covers.");
        CensorshipMaximumDistance = BindRange(config, "14.7 - Censorship", "Maximum Distance", 80f, 10f, 120f,
            "Farthest distance in metres at which visible faces are censored.");
        CensorshipBlockCount = BindRange(config, "14.7 - Censorship", "Mosaic Blocks", 8f, 5f, 18f,
            "Pixel blocks across the mosaic at close range. Lower = larger blocks. The block count automatically decreases with distance.");

        DamageEffectsEnabled = config.Bind(
            "15 - Bodycam Damage Effects",
            "Enabled",
            true,
            "Shows up to two neutral faceshield-style impact marks on the bodycam lens after combat hits.");
        LensDamageStrength = BindRange(config, "15 - Bodycam Damage Effects", "Lens Cracks Visibility", 0.45f, 0f, 1f,
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
