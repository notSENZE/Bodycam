using BepInEx.Logging;
using EFT;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using PostProcessChromaticAberration = UnityEngine.Rendering.PostProcessing.ChromaticAberration;

namespace SenzeBodycam;

internal sealed class BodyCameraController
{
    private readonly ModConfig _settings;
    private readonly ManualLogSource _logger;
    private Player _player;
    private Camera _camera;
    private Quaternion _followRotation;
    private Vector3 _followAngularVelocity;
    private bool _hasFollowRotation;
    private float _activationBlend;
    private float _adsBlend;
    private float _movementNoiseTime;
    private float _gaitPhase;
    private Vector3 _movementPosition;
    private Vector3 _movementRotation;
    private Vector3 _stabilizedMountOffset;
    private bool _hasStabilizedMountPosition;
    private Vector3 _inertiaPosition;
    private Vector3 _inertiaPositionVelocity;
    private Vector3 _inertiaRotation;
    private Vector3 _inertiaRotationVelocity;
    private Vector3 _smoothedLocalVelocity;
    private Vector3 _previousSmoothedLocalVelocity;
    private bool _hasInertiaSample;
    private Vector3 _shotRotation;
    private Vector3 _shotRotationVelocity;
    private float _coverResponse;
    private float _coverResponseVelocity;
    private int _lastCoverProbeFrame = -100;
    private int _lastCameraFrame = -100;
    private Vignette _vignette;
    private bool _vignetteWasActive;
    private bool _vignetteWasEnabled;
    private VignetteMode _vignetteMode;
    private Color _vignetteColor;
    private Vector2 _vignetteCenter;
    private float _vignetteIntensity;
    private float _vignetteSmoothness;
    private float _vignetteRoundness;
    private bool _vignetteRounded;
    private Texture _vignetteMask;
    private float _vignetteOpacity;
    private LensDistortion _lensDistortion;
    private bool _lensDistortionWasActive;
    private bool _lensDistortionWasEnabled;
    private float _lensDistortionIntensity;
    private float _lensDistortionCenterX;
    private float _lensDistortionCenterY;
    private float _lensDistortionIntensityX;
    private float _lensDistortionIntensityY;
    private float _lensDistortionScale;
    private PostProcessChromaticAberration _chromaticAberration;
    private bool _chromaticAberrationWasActive;
    private bool _chromaticAberrationWasEnabled;
    private float _chromaticAberrationIntensity;
    private bool _chromaticAberrationFastMode;
    private Bloom _bloom;
    private bool _bloomWasActive;
    private bool _bloomWasEnabled;
    private float _bloomIntensity;
    private float _bloomThreshold;
    private float _bloomSoftKnee;
    private float _bloomDiffusion;
    private bool _bloomFastMode;
    private DepthOfField _depthOfField;
    private bool _depthOfFieldWasActive;
    private bool _depthOfFieldWasEnabled;
    private float _depthOfFieldFocusDistance;
    private float _depthOfFieldAperture;
    private float _depthOfFieldFocalLength;
    private KernelSize _depthOfFieldKernelSize;
    private MotionBlur _motionBlur;
    private bool _motionBlurWasActive;
    private bool _motionBlurWasEnabled;
    private float _motionBlurShutterAngle;
    private int _motionBlurSampleCount;
    private Grain _grain;
    private bool _grainWasActive;
    private bool _grainWasEnabled;
    private bool _grainColored;
    private float _grainIntensity;
    private float _grainSize;
    private float _grainLuminanceContribution;
    private ColorGrading _colorGrading;
    private bool _colorGradingWasActive;
    private bool _colorGradingWasEnabled;
    private float _colorGradingPostExposure;

    internal Player OverlayPlayer => _player;

    internal Camera OverlayCamera => _camera;

    internal bool IsActiveFor(Player player)
    {
        return player != null
            && ReferenceEquals(player, _player)
            && _settings.ModEnabled.Value
            && _settings.BodyCameraEnabled.Value
            && Time.frameCount - _lastCameraFrame <= 2;
    }

    internal bool ShouldDrawOverlay =>
        _settings.ModEnabled.Value
        && _settings.BodyCameraEnabled.Value
        && _player != null
        && Time.frameCount - _lastCameraFrame <= 2;

    internal BodyCameraController(ModConfig settings, ManualLogSource logger)
    {
        _settings = settings;
        _logger = logger;
    }

    internal void UpdateInput()
    {
        if (!_settings.ModEnabled.Value || !ShortcutInput.IsDown(_settings.ToggleBodyCameraKey.Value))
        {
            return;
        }

        _settings.BodyCameraEnabled.Value = !_settings.BodyCameraEnabled.Value;
        _logger.LogInfo($"Bodycam {(_settings.BodyCameraEnabled.Value ? "enabled" : "disabled")}.");
    }

    internal void Apply(PlayerCameraController cameraController)
    {
        var player = cameraController?.Player;
        var camera = cameraController?.Camera;
        if (player == null
            || camera == null
            || !player.IsYourPlayer
            || player.PointOfView != EPointOfView.FirstPerson)
        {
            Reset();
            return;
        }

        if (!ReferenceEquals(_player, player) || !ReferenceEquals(_camera, camera))
        {
            BeginTracking(player, camera);
        }
        _lastCameraFrame = Time.frameCount;
        var active = _settings.ModEnabled.Value && _settings.BodyCameraEnabled.Value;

        var deltaTime = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
        if (deltaTime <= 0f)
        {
            return;
        }

        var cameraTransform = camera.transform;
        var basePosition = cameraTransform.position;
        var baseRotation = cameraTransform.rotation;
        if (!active)
        {
            PrepareForActivation(baseRotation);
            return;
        }

        var ribcage = player.PlayerBones?.Ribcage?.Original;
        if (ribcage == null)
        {
            return;
        }

        var aiming = player.ProceduralWeaponAnimation?.IsAiming == true;
        if (_activationBlend <= 0f && aiming)
        {
            _adsBlend = GetAdsTarget(aiming);
        }
        var adsTarget = GetAdsTarget(aiming);
        _adsBlend = ExpLerp(_adsBlend, adsTarget, 8f, deltaTime);
        _activationBlend = ExpLerp(_activationBlend, 1f, 10f, deltaTime);

        var effectiveAdsBlend = _adsBlend;

        if (!_hasFollowRotation)
        {
            _followRotation = baseRotation;
            _hasFollowRotation = true;
        }

        UpdateRotationSpring(baseRotation, deltaTime);

        var velocity = player.MovementContext.Velocity;
        UpdateMovement(baseRotation, velocity, deltaTime);
        UpdateMotionInertia(baseRotation, velocity, deltaTime);
        UpdateShotKick(deltaTime);

        var bodyForward = Vector3.ProjectOnPlane(player.Transform.forward, Vector3.up);
        if (bodyForward.sqrMagnitude < 0.001f)
        {
            bodyForward = Vector3.ProjectOnPlane(baseRotation * Vector3.forward, Vector3.up);
        }
        bodyForward.Normalize();

        var bodyRight = Vector3.Cross(Vector3.up, bodyForward).normalized;
        var animatedBodyPosition = ribcage.position
            + bodyRight * _settings.CameraSideways.Value
            + Vector3.up * _settings.CameraVertical.Value
            + bodyForward * _settings.CameraForward.Value;
        var bodyPosition = GetStabilizedMountPosition(
            player,
            basePosition,
            animatedBodyPosition,
            velocity,
            deltaTime);
        var positionBeforeActivation = Vector3.Lerp(bodyPosition, basePosition, effectiveAdsBlend);
        positionBeforeActivation += baseRotation * (_movementPosition + _inertiaPosition);
        cameraTransform.position = Vector3.Lerp(basePosition, positionBeforeActivation, _activationBlend);

        var bodyRotation = _followRotation;
        var localRotation = _movementRotation + _inertiaRotation + _shotRotation;
        var bodyCameraRotation = bodyRotation * Quaternion.Euler(localRotation);
        cameraTransform.rotation = Quaternion.Slerp(baseRotation, bodyCameraRotation, _activationBlend);
    }

    internal void AddShotKick(Player player)
    {
        if (player == null
            || !ReferenceEquals(player, _player)
            || !_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value)
        {
            return;
        }

        var strength = _settings.ShotReaction.Value;
        var yaw = Random.Range(-0.1971831f, 0.1971831f) * strength;
        _shotRotation += new Vector3(-0.308169f * strength, yaw, 0f);
        _shotRotation.x = Mathf.Clamp(_shotRotation.x, -4f, 0f);
        _shotRotation.y = Mathf.Clamp(_shotRotation.y, -2f, 2f);
    }

    internal bool TryGetInteractionRay(Player player, out Ray interactionRay)
    {
        interactionRay = default;
        if (!IsActiveFor(player) || _camera == null)
        {
            return false;
        }

        var cameraTransform = _camera.transform;
        interactionRay = new Ray(
            cameraTransform.position - cameraTransform.forward * EFTHardSettings.Instance.BEHIND_CAST,
            cameraTransform.forward);
        return true;
    }

    internal void ApplyWeaponCornerPeek(
        EFT.Animations.ProceduralWeaponAnimation weaponAnimation)
    {
        if (!_settings.CornerPeekEnabled.Value
            || _settings.CornerPeekStrength.Value <= 0.001f
            || weaponAnimation?.IsAiming == true
            || !CanApplyWeaponCoverMotion(weaponAnimation))
        {
            ResetCoverResponse();
            return;
        }

        var weaponRoot = weaponAnimation.HandsContainer?.WeaponRootAnim;
        if (weaponRoot == null)
        {
            return;
        }

        UpdateCoverResponse();
        var response = _coverResponse * Mathf.Clamp(_settings.CornerPeekStrength.Value, 0f, 2f);
        if (Mathf.Abs(response) < 0.001f)
        {
            return;
        }

        var pivot = weaponRoot.TransformPoint(new Vector3(0f, -0.2f, 0f));
        var rotation = new Vector3(0f, response * 9.5f, -response * 10.8f);
        weaponAnimation.RotateAround(weaponRoot, pivot, rotation);
        weaponAnimation.Offset = weaponRoot.localPosition;
    }

    private static float GetCoverProximity(Vector3 origin, Vector3 forward, Vector3 side)
    {
        const float probeRadius = 0.1f;
        var forwardProximity = GetObstacleProximity(
            origin + side * 0.24f,
            forward,
            probeRadius,
            1.35f);
        var diagonalProximity = GetObstacleProximity(
            origin + side * 0.08f,
            (forward + side * 0.55f).normalized,
            probeRadius,
            1.15f) * 0.97f;
        var sideProximity = GetObstacleProximity(
            origin + forward * 0.14f,
            side,
            probeRadius,
            0.65f) * 0.84f;

        return Mathf.Max(forwardProximity, diagonalProximity, sideProximity);
    }

    private static float GetObstacleProximity(
        Vector3 origin,
        Vector3 direction,
        float radius,
        float distance)
    {
        if (!Physics.SphereCast(
                origin,
                radius,
                direction,
                out var hit,
                distance,
                LayersMaskController.HighPolyWithTerrainMask,
                QueryTriggerInteraction.Ignore))
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(hit.distance / distance);
    }

    private bool CanApplyWeaponCoverMotion(
        EFT.Animations.ProceduralWeaponAnimation weaponAnimation)
    {
        return weaponAnimation != null
            && _player != null
            && ReferenceEquals(weaponAnimation, _player.ProceduralWeaponAnimation)
            && IsActiveFor(_player)
            && !weaponAnimation.IsMountedState
            && _camera != null;
    }

    private void UpdateCoverResponse()
    {
        if (_lastCoverProbeFrame == Time.frameCount)
        {
            return;
        }

        _lastCoverProbeFrame = Time.frameCount;
        var cameraTransform = _camera.transform;
        var forward = cameraTransform.forward;
        var right = cameraTransform.right;
        var origin = cameraTransform.position + forward * 0.04f;

        var leftProximity = GetCoverProximity(origin, forward, -right);
        var rightProximity = GetCoverProximity(origin, forward, right);
        var proximityDifference = leftProximity - rightProximity;
        var targetResponse = Mathf.Abs(proximityDifference) <= 0.025f
            ? 0f
            : Mathf.Sign(proximityDifference)
                * Mathf.InverseLerp(0.025f, 0.45f, Mathf.Abs(proximityDifference));

        var deltaTime = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
        var smoothTime = Mathf.Abs(targetResponse) > Mathf.Abs(_coverResponse) ? 0.04f : 0.18f;
        _coverResponse = Mathf.SmoothDamp(
            _coverResponse,
            targetResponse,
            ref _coverResponseVelocity,
            smoothTime,
            12f,
            deltaTime);
    }

    private void ResetCoverResponse()
    {
        _coverResponse = 0f;
        _coverResponseVelocity = 0f;
        _lastCoverProbeFrame = -100;
    }

    internal void Reset()
    {
        RestoreLensDistortion(true);
        RestoreVignette(true);
        RestoreChromaticAberration(true);
        RestoreBloom(true);
        RestoreDepthOfField(true);
        RestoreMotionBlur(true);
        RestoreGrain(true);
        RestoreColorGrading(true);
        _player = null;
        _camera = null;
        _followRotation = Quaternion.identity;
        _followAngularVelocity = Vector3.zero;
        _hasFollowRotation = false;
        _activationBlend = 0f;
        _adsBlend = 0f;
        _movementNoiseTime = 0f;
        _gaitPhase = 0f;
        _movementPosition = Vector3.zero;
        _movementRotation = Vector3.zero;
        _stabilizedMountOffset = Vector3.zero;
        _hasStabilizedMountPosition = false;
        _inertiaPosition = Vector3.zero;
        _inertiaPositionVelocity = Vector3.zero;
        _inertiaRotation = Vector3.zero;
        _inertiaRotationVelocity = Vector3.zero;
        _smoothedLocalVelocity = Vector3.zero;
        _previousSmoothedLocalVelocity = Vector3.zero;
        _hasInertiaSample = false;
        _shotRotation = Vector3.zero;
        _shotRotationVelocity = Vector3.zero;
        _coverResponse = 0f;
        _coverResponseVelocity = 0f;
        _lastCoverProbeFrame = -100;
        _lastCameraFrame = -100;
    }

    internal void ApplyLensDistortion(PostProcessLayer postProcessLayer, Camera camera)
    {
        if (postProcessLayer == null
            || !ReferenceEquals(camera, _camera)
            || Time.frameCount - _lastCameraFrame > 2)
        {
            return;
        }

        ApplyLensDistortion(postProcessLayer);
        ApplyVignette(postProcessLayer);
        ApplyChromaticAberration(postProcessLayer);
        ApplyBloom(postProcessLayer);
        ApplyDepthOfField(postProcessLayer);
        ApplyMotionBlur(postProcessLayer);
        ApplyGrain(postProcessLayer);
        ApplyColorGrading(postProcessLayer);
    }

    private void ApplyLensDistortion(PostProcessLayer postProcessLayer)
    {
        var lensDistortion = postProcessLayer.GetSettings<LensDistortion>();
        if (lensDistortion == null)
        {
            return;
        }

        TrackLensDistortion(lensDistortion);
        if (!_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || !_settings.FisheyeEffectEnabled.Value
            || _settings.FisheyeStrength.Value <= 0.001f)
        {
            RestoreLensDistortion(false);
            return;
        }

        lensDistortion.active = true;
        lensDistortion.enabled.value = true;
        lensDistortion.centerX.value = 0f;
        lensDistortion.centerY.value = 0f;
        lensDistortion.intensityX.value = 1f;
        lensDistortion.intensityY.value = 1f;
        lensDistortion.intensity.value = _settings.FisheyeStrength.Value;
        lensDistortion.scale.value = _settings.FisheyeEdgeCrop.Value;
    }

    private void ApplyVignette(PostProcessLayer postProcessLayer)
    {
        var vignette = postProcessLayer.GetSettings<Vignette>();
        if (vignette == null)
        {
            return;
        }

        TrackVignette(vignette);
        var strength = Mathf.Clamp(_settings.EdgeVignetteStrength.Value, 0f, 0.45f);
        if (!_settings.ModEnabled.Value || !_settings.BodyCameraEnabled.Value || strength <= 0.001f)
        {
            RestoreVignette(false);
            return;
        }

        vignette.active = true;
        vignette.enabled.value = true;
        vignette.mode.value = VignetteMode.Classic;
        vignette.color.value = Color.black;
        vignette.center.value = new Vector2(0.5f, 0.5f);
        vignette.intensity.value = Mathf.Min(0.6f, strength * 1.2f);
        vignette.smoothness.value = 0.1f;
        vignette.roundness.value = 1f;
        vignette.rounded.value = true;
    }

    private void ApplyChromaticAberration(PostProcessLayer postProcessLayer)
    {
        var chromaticAberration = postProcessLayer.GetSettings<PostProcessChromaticAberration>();
        if (chromaticAberration == null)
        {
            return;
        }

        TrackChromaticAberration(chromaticAberration);
        var strength = _settings.ChromaticAberrationStrength.Value;
        if (!_settings.ModEnabled.Value || !_settings.BodyCameraEnabled.Value || strength <= 0.001f)
        {
            RestoreChromaticAberration(false);
            return;
        }

        chromaticAberration.active = true;
        chromaticAberration.enabled.value = true;
        chromaticAberration.intensity.value = Mathf.Clamp01(strength);
        chromaticAberration.fastMode.value = false;
    }

    private void ApplyBloom(PostProcessLayer postProcessLayer)
    {
        var bloom = postProcessLayer.GetSettings<Bloom>();
        if (bloom == null)
        {
            return;
        }

        TrackBloom(bloom);
        var strength = _settings.HighlightBloomStrength.Value;
        if (!_settings.ModEnabled.Value || !_settings.BodyCameraEnabled.Value || strength <= 0.001f)
        {
            RestoreBloom(false);
            return;
        }

        bloom.active = true;
        bloom.enabled.value = true;
        bloom.intensity.value = strength;
        bloom.threshold.value = 1.08f;
        bloom.softKnee.value = 0.55f;
        bloom.diffusion.value = 5f;
        bloom.fastMode.value = true;
    }

    private void ApplyDepthOfField(PostProcessLayer postProcessLayer)
    {
        var depthOfField = postProcessLayer.GetSettings<DepthOfField>();
        if (depthOfField == null)
        {
            return;
        }

        TrackDepthOfField(depthOfField);
        var strength = Mathf.Clamp01(_settings.NearFocusBlurStrength.Value);
        var aiming = _player?.ProceduralWeaponAnimation?.IsAiming == true;
        if (!_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || aiming
            || strength <= 0.001f)
        {
            RestoreDepthOfField(false);
            return;
        }

        depthOfField.active = true;
        depthOfField.enabled.value = true;
        depthOfField.focusDistance.value = 8f;
        depthOfField.aperture.value = Mathf.Lerp(16f, 3.5f, strength);
        depthOfField.focalLength.value = Mathf.Lerp(18f, 32f, strength);
        depthOfField.kernelSize.value = KernelSize.Small;
    }

    private void ApplyMotionBlur(PostProcessLayer postProcessLayer)
    {
        var motionBlur = postProcessLayer.GetSettings<MotionBlur>();
        if (motionBlur == null)
        {
            return;
        }

        TrackMotionBlur(motionBlur);
        var strength = Mathf.Clamp01(_settings.MotionBlurStrength.Value);
        if (!_settings.ModEnabled.Value || !_settings.BodyCameraEnabled.Value || strength <= 0.001f)
        {
            RestoreMotionBlur(false);
            return;
        }

        motionBlur.active = true;
        motionBlur.enabled.value = true;
        motionBlur.shutterAngle.value = Mathf.Lerp(0f, 180f, strength);
        motionBlur.sampleCount.value = Mathf.RoundToInt(Mathf.Lerp(6f, 14f, strength));
    }

    private void ApplyGrain(PostProcessLayer postProcessLayer)
    {
        var grain = postProcessLayer.GetSettings<Grain>();
        if (grain == null)
        {
            return;
        }

        TrackGrain(grain);
        var strength = Mathf.Clamp01(_settings.SensorNoiseStrength.Value);
        if (!_settings.ModEnabled.Value || !_settings.BodyCameraEnabled.Value || strength <= 0.001f)
        {
            RestoreGrain(false);
            return;
        }

        grain.active = true;
        grain.enabled.value = true;
        grain.colored.value = false;
        grain.intensity.value = strength * 0.55f;
        grain.size.value = 1.1f;
        grain.lumContrib.value = 0.65f;
    }

    private void ApplyColorGrading(PostProcessLayer postProcessLayer)
    {
        var colorGrading = postProcessLayer.GetSettings<ColorGrading>();
        if (colorGrading == null)
        {
            return;
        }

        TrackColorGrading(colorGrading);
        var exposure = _settings.CameraExposure.Value;
        if (!_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || Mathf.Abs(exposure) <= 0.001f)
        {
            RestoreColorGrading(false);
            return;
        }

        colorGrading.active = true;
        colorGrading.enabled.value = true;
        colorGrading.postExposure.value = _colorGradingPostExposure + exposure;
    }

    private void TrackLensDistortion(LensDistortion lensDistortion)
    {
        if (ReferenceEquals(_lensDistortion, lensDistortion))
        {
            return;
        }

        RestoreLensDistortion(true);
        _lensDistortion = lensDistortion;
        _lensDistortionWasActive = lensDistortion.active;
        _lensDistortionWasEnabled = lensDistortion.enabled.value;
        _lensDistortionIntensity = lensDistortion.intensity.value;
        _lensDistortionCenterX = lensDistortion.centerX.value;
        _lensDistortionCenterY = lensDistortion.centerY.value;
        _lensDistortionIntensityX = lensDistortion.intensityX.value;
        _lensDistortionIntensityY = lensDistortion.intensityY.value;
        _lensDistortionScale = lensDistortion.scale.value;
    }

    private void TrackVignette(Vignette vignette)
    {
        if (ReferenceEquals(_vignette, vignette))
        {
            return;
        }

        RestoreVignette(true);
        _vignette = vignette;
        _vignetteWasActive = vignette.active;
        _vignetteWasEnabled = vignette.enabled.value;
        _vignetteMode = vignette.mode.value;
        _vignetteColor = vignette.color.value;
        _vignetteCenter = vignette.center.value;
        _vignetteIntensity = vignette.intensity.value;
        _vignetteSmoothness = vignette.smoothness.value;
        _vignetteRoundness = vignette.roundness.value;
        _vignetteRounded = vignette.rounded.value;
        _vignetteMask = vignette.mask.value;
        _vignetteOpacity = vignette.opacity.value;
    }

    private void RestoreVignette(bool forget)
    {
        if (_vignette != null)
        {
            _vignette.active = _vignetteWasActive;
            _vignette.enabled.value = _vignetteWasEnabled;
            _vignette.mode.value = _vignetteMode;
            _vignette.color.value = _vignetteColor;
            _vignette.center.value = _vignetteCenter;
            _vignette.intensity.value = _vignetteIntensity;
            _vignette.smoothness.value = _vignetteSmoothness;
            _vignette.roundness.value = _vignetteRoundness;
            _vignette.rounded.value = _vignetteRounded;
            _vignette.mask.value = _vignetteMask;
            _vignette.opacity.value = _vignetteOpacity;
        }

        if (forget)
        {
            _vignette = null;
        }
    }

    private void RestoreLensDistortion(bool forget)
    {
        if (_lensDistortion != null)
        {
            _lensDistortion.active = _lensDistortionWasActive;
            _lensDistortion.enabled.value = _lensDistortionWasEnabled;
            _lensDistortion.intensity.value = _lensDistortionIntensity;
            _lensDistortion.centerX.value = _lensDistortionCenterX;
            _lensDistortion.centerY.value = _lensDistortionCenterY;
            _lensDistortion.intensityX.value = _lensDistortionIntensityX;
            _lensDistortion.intensityY.value = _lensDistortionIntensityY;
            _lensDistortion.scale.value = _lensDistortionScale;
        }

        if (forget)
        {
            _lensDistortion = null;
        }
    }

    private void TrackChromaticAberration(PostProcessChromaticAberration chromaticAberration)
    {
        if (ReferenceEquals(_chromaticAberration, chromaticAberration))
        {
            return;
        }

        RestoreChromaticAberration(true);
        _chromaticAberration = chromaticAberration;
        _chromaticAberrationWasActive = chromaticAberration.active;
        _chromaticAberrationWasEnabled = chromaticAberration.enabled.value;
        _chromaticAberrationIntensity = chromaticAberration.intensity.value;
        _chromaticAberrationFastMode = chromaticAberration.fastMode.value;
    }

    private void RestoreChromaticAberration(bool forget)
    {
        if (_chromaticAberration != null)
        {
            _chromaticAberration.active = _chromaticAberrationWasActive;
            _chromaticAberration.enabled.value = _chromaticAberrationWasEnabled;
            _chromaticAberration.intensity.value = _chromaticAberrationIntensity;
            _chromaticAberration.fastMode.value = _chromaticAberrationFastMode;
        }

        if (forget)
        {
            _chromaticAberration = null;
        }
    }

    private void TrackBloom(Bloom bloom)
    {
        if (ReferenceEquals(_bloom, bloom))
        {
            return;
        }

        RestoreBloom(true);
        _bloom = bloom;
        _bloomWasActive = bloom.active;
        _bloomWasEnabled = bloom.enabled.value;
        _bloomIntensity = bloom.intensity.value;
        _bloomThreshold = bloom.threshold.value;
        _bloomSoftKnee = bloom.softKnee.value;
        _bloomDiffusion = bloom.diffusion.value;
        _bloomFastMode = bloom.fastMode.value;
    }

    private void RestoreBloom(bool forget)
    {
        if (_bloom != null)
        {
            _bloom.active = _bloomWasActive;
            _bloom.enabled.value = _bloomWasEnabled;
            _bloom.intensity.value = _bloomIntensity;
            _bloom.threshold.value = _bloomThreshold;
            _bloom.softKnee.value = _bloomSoftKnee;
            _bloom.diffusion.value = _bloomDiffusion;
            _bloom.fastMode.value = _bloomFastMode;
        }

        if (forget)
        {
            _bloom = null;
        }
    }

    private void TrackDepthOfField(DepthOfField depthOfField)
    {
        if (ReferenceEquals(_depthOfField, depthOfField))
        {
            return;
        }

        RestoreDepthOfField(true);
        _depthOfField = depthOfField;
        _depthOfFieldWasActive = depthOfField.active;
        _depthOfFieldWasEnabled = depthOfField.enabled.value;
        _depthOfFieldFocusDistance = depthOfField.focusDistance.value;
        _depthOfFieldAperture = depthOfField.aperture.value;
        _depthOfFieldFocalLength = depthOfField.focalLength.value;
        _depthOfFieldKernelSize = depthOfField.kernelSize.value;
    }

    private void RestoreDepthOfField(bool forget)
    {
        if (_depthOfField != null)
        {
            _depthOfField.active = _depthOfFieldWasActive;
            _depthOfField.enabled.value = _depthOfFieldWasEnabled;
            _depthOfField.focusDistance.value = _depthOfFieldFocusDistance;
            _depthOfField.aperture.value = _depthOfFieldAperture;
            _depthOfField.focalLength.value = _depthOfFieldFocalLength;
            _depthOfField.kernelSize.value = _depthOfFieldKernelSize;
        }

        if (forget)
        {
            _depthOfField = null;
        }
    }

    private void TrackMotionBlur(MotionBlur motionBlur)
    {
        if (ReferenceEquals(_motionBlur, motionBlur))
        {
            return;
        }

        RestoreMotionBlur(true);
        _motionBlur = motionBlur;
        _motionBlurWasActive = motionBlur.active;
        _motionBlurWasEnabled = motionBlur.enabled.value;
        _motionBlurShutterAngle = motionBlur.shutterAngle.value;
        _motionBlurSampleCount = motionBlur.sampleCount.value;
    }

    private void RestoreMotionBlur(bool forget)
    {
        if (_motionBlur != null)
        {
            _motionBlur.active = _motionBlurWasActive;
            _motionBlur.enabled.value = _motionBlurWasEnabled;
            _motionBlur.shutterAngle.value = _motionBlurShutterAngle;
            _motionBlur.sampleCount.value = _motionBlurSampleCount;
        }

        if (forget)
        {
            _motionBlur = null;
        }
    }

    private void TrackGrain(Grain grain)
    {
        if (ReferenceEquals(_grain, grain))
        {
            return;
        }

        RestoreGrain(true);
        _grain = grain;
        _grainWasActive = grain.active;
        _grainWasEnabled = grain.enabled.value;
        _grainColored = grain.colored.value;
        _grainIntensity = grain.intensity.value;
        _grainSize = grain.size.value;
        _grainLuminanceContribution = grain.lumContrib.value;
    }

    private void RestoreGrain(bool forget)
    {
        if (_grain != null)
        {
            _grain.active = _grainWasActive;
            _grain.enabled.value = _grainWasEnabled;
            _grain.colored.value = _grainColored;
            _grain.intensity.value = _grainIntensity;
            _grain.size.value = _grainSize;
            _grain.lumContrib.value = _grainLuminanceContribution;
        }

        if (forget)
        {
            _grain = null;
        }
    }

    private void TrackColorGrading(ColorGrading colorGrading)
    {
        if (ReferenceEquals(_colorGrading, colorGrading))
        {
            return;
        }

        RestoreColorGrading(true);
        _colorGrading = colorGrading;
        _colorGradingWasActive = colorGrading.active;
        _colorGradingWasEnabled = colorGrading.enabled.value;
        _colorGradingPostExposure = colorGrading.postExposure.value;
    }

    private void RestoreColorGrading(bool forget)
    {
        if (_colorGrading != null)
        {
            _colorGrading.active = _colorGradingWasActive;
            _colorGrading.enabled.value = _colorGradingWasEnabled;
            _colorGrading.postExposure.value = _colorGradingPostExposure;
        }

        if (forget)
        {
            _colorGrading = null;
        }
    }

    private void BeginTracking(Player player, Camera camera)
    {
        Reset();
        _player = player;
        _camera = camera;
        PrepareForActivation(camera.transform.rotation);
    }

    private void PrepareForActivation(Quaternion baseRotation)
    {
        _followRotation = baseRotation;
        _followAngularVelocity = Vector3.zero;
        _hasFollowRotation = true;
        _activationBlend = 0f;
        _adsBlend = 0f;
        _movementNoiseTime = 0f;
        _gaitPhase = 0f;
        _movementPosition = Vector3.zero;
        _movementRotation = Vector3.zero;
        _stabilizedMountOffset = Vector3.zero;
        _hasStabilizedMountPosition = false;
        _inertiaPosition = Vector3.zero;
        _inertiaPositionVelocity = Vector3.zero;
        _inertiaRotation = Vector3.zero;
        _inertiaRotationVelocity = Vector3.zero;
        _smoothedLocalVelocity = Vector3.zero;
        _previousSmoothedLocalVelocity = Vector3.zero;
        _hasInertiaSample = false;
        _shotRotation = Vector3.zero;
        _shotRotationVelocity = Vector3.zero;
        _coverResponse = 0f;
        _coverResponseVelocity = 0f;
        _lastCoverProbeFrame = -100;
    }

    private void UpdateRotationSpring(Quaternion targetRotation, float deltaTime)
    {
        if (!_hasFollowRotation)
        {
            _followRotation = targetRotation;
            _followAngularVelocity = Vector3.zero;
            _hasFollowRotation = true;
            return;
        }

        var cameraWeight = _settings.CameraWeight.Value;
        if (cameraWeight <= 0.001f)
        {
            _followRotation = targetRotation;
            _followAngularVelocity = Vector3.zero;
            return;
        }

        var angularFrequency = Mathf.Lerp(28f, 6.4f, cameraWeight);
        var damping = Mathf.Lerp(1.05f, 0.82f, cameraWeight);
        var maximumLag = 22f * cameraWeight;
        var remainingTime = deltaTime;
        while (remainingTime > 0f)
        {
            var step = Mathf.Min(remainingTime, 1f / 120f);
            remainingTime -= step;

            var errorRotation = targetRotation * Quaternion.Inverse(_followRotation);
            errorRotation.ToAngleAxis(out var errorAngle, out var errorAxis);
            if (errorAngle > 180f)
            {
                errorAngle -= 360f;
            }

            var error = Vector3.zero;
            if (Mathf.Abs(errorAngle) > 0.0001f && errorAxis.sqrMagnitude > 0.0001f)
            {
                error = errorAxis.normalized * (errorAngle * Mathf.Deg2Rad);
            }

            var angularAcceleration = error * (angularFrequency * angularFrequency)
                - _followAngularVelocity * (2f * damping * angularFrequency);
            _followAngularVelocity += angularAcceleration * step;
            _followAngularVelocity = Vector3.ClampMagnitude(_followAngularVelocity, 12f);

            var rotationStep = _followAngularVelocity * step;
            var rotationStepAngle = rotationStep.magnitude;
            if (rotationStepAngle > 0.000001f)
            {
                _followRotation = Quaternion.AngleAxis(
                    rotationStepAngle * Mathf.Rad2Deg,
                    rotationStep / rotationStepAngle) * _followRotation;
            }
        }

        if (Quaternion.Angle(_followRotation, targetRotation) > maximumLag)
        {
            _followRotation = Quaternion.RotateTowards(targetRotation, _followRotation, maximumLag);
        }
    }

    private Vector3 GetStabilizedMountPosition(
        Player player,
        Vector3 vanillaCameraPosition,
        Vector3 animatedBodyPosition,
        Vector3 velocity,
        float deltaTime)
    {
        var playerTransform = player.Transform;
        var mountOffset = playerTransform.InverseTransformVector(
            animatedBodyPosition - vanillaCameraPosition);
        if (!_hasStabilizedMountPosition || mountOffset.sqrMagnitude > 2.25f)
        {
            _stabilizedMountOffset = mountOffset;
            _hasStabilizedMountPosition = true;
            return vanillaCameraPosition
                + playerTransform.TransformVector(_stabilizedMountOffset);
        }

        var horizontalSmoothing = 1f - Mathf.Exp(-10f * deltaTime);
        _stabilizedMountOffset.x = Mathf.Lerp(
            _stabilizedMountOffset.x,
            mountOffset.x,
            horizontalSmoothing);
        _stabilizedMountOffset.z = Mathf.Lerp(
            _stabilizedMountOffset.z,
            mountOffset.z,
            horizontalSmoothing);

        var horizontalVelocity = velocity;
        horizontalVelocity.y = 0f;
        var verticalDifference = mountOffset.y - _stabilizedMountOffset.y;
        var isMoving = horizontalVelocity.sqrMagnitude > 0.0225f;
        if (!isMoving || Mathf.Abs(verticalDifference) > 0.06f)
        {
            var verticalFollowSpeed = isMoving ? 3f : 10f;
            var verticalSmoothing = 1f - Mathf.Exp(-verticalFollowSpeed * deltaTime);
            _stabilizedMountOffset.y = Mathf.Lerp(
                _stabilizedMountOffset.y,
                mountOffset.y,
                verticalSmoothing);
        }

        return vanillaCameraPosition
            + playerTransform.TransformVector(_stabilizedMountOffset);
    }

    private void UpdateMovement(Quaternion baseRotation, Vector3 velocity, float deltaTime)
    {
        var horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        var speed = horizontalVelocity.magnitude;
        var speedFactor = Mathf.Clamp01(speed / 4.5f);

        var targetPosition = Vector3.zero;
        var targetRotation = Vector3.zero;
        if (speed > 0.15f)
        {
            _movementNoiseTime += deltaTime * (0.85f + speed * 0.42f);
            var localVelocity = Quaternion.Inverse(baseRotation) * horizontalVelocity;
            var strafe = Mathf.Clamp(localVelocity.x / 5f, -1f, 1f);
            var strength = _settings.MovementIntensity.Value * 1.8f * speedFactor;

            var lateralNoise = SignedPerlin(13.7f, _movementNoiseTime * 0.93f);
            var verticalNoise = SignedPerlin(29.1f, _movementNoiseTime * 1.27f);
            var forwardNoise = SignedPerlin(47.3f, _movementNoiseTime * 0.71f);
            var pitchNoise = SignedPerlin(61.9f, _movementNoiseTime * 1.11f);
            var yawNoise = SignedPerlin(79.4f, _movementNoiseTime * 0.83f);
            var rollNoise = SignedPerlin(97.8f, _movementNoiseTime * 0.67f);

            targetPosition.x = lateralNoise * 0.0022f * strength;
            targetPosition.y = verticalNoise * 0.00035f * strength;
            targetPosition.z = forwardNoise * 0.0011f * strength;
            targetRotation.x = pitchNoise * 0.22f * strength;
            targetRotation.y = yawNoise * 0.15f * strength;
            targetRotation.z = (rollNoise * 0.58f - strafe * 0.22f) * strength;

            var movementAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 2.4f, speed));
            var runAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.3f, 6f, speed));
            var verticalMovement = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.5f, 2f, Mathf.Abs(velocity.y)));
            var strideFrequency = Mathf.Lerp(1.55f, 2.55f, runAmount);
            _gaitPhase += deltaTime * strideFrequency * Mathf.PI * 2f;
            if (_gaitPhase > Mathf.PI * 2000f)
            {
                _gaitPhase -= Mathf.PI * 2000f;
            }

            var gaitVariation = 1f + SignedPerlin(113.2f, _movementNoiseTime * 0.24f) * 0.08f;
            var gaitStrength = _settings.MovementIntensity.Value
                * movementAmount
                * verticalMovement
                * gaitVariation;
            var verticalStep = Mathf.Sin(_gaitPhase);
            var alternatingStep = Mathf.Sin(_gaitPhase * 0.5f);

            targetPosition.x += alternatingStep * Mathf.Lerp(0.00045f, 0.0009f, runAmount) * gaitStrength;
            targetPosition.y += verticalStep * Mathf.Lerp(0.0018f, 0.0034f, runAmount) * gaitStrength;
            targetRotation.x += -verticalStep * Mathf.Lerp(0.12f, 0.24f, runAmount) * gaitStrength;
            targetRotation.z += alternatingStep * Mathf.Lerp(0.16f, 0.34f, runAmount) * gaitStrength;
        }

        var smoothing = 1f - Mathf.Exp(-8.5f * deltaTime);
        _movementPosition = Vector3.Lerp(_movementPosition, targetPosition, smoothing);
        _movementRotation = Vector3.Lerp(_movementRotation, targetRotation, smoothing);
    }

    private static float SignedPerlin(float seed, float time)
    {
        return Mathf.PerlinNoise(seed, time) * 2f - 1f;
    }

    private void UpdateShotKick(float deltaTime)
    {
        _shotRotation = Vector3.SmoothDamp(
            _shotRotation,
            Vector3.zero,
            ref _shotRotationVelocity,
            0.11f,
            Mathf.Infinity,
            deltaTime);
    }

    private void UpdateMotionInertia(Quaternion baseRotation, Vector3 velocity, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane(baseRotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward).normalized;

        var horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        var localVelocity = new Vector3(
            Vector3.Dot(horizontalVelocity, right),
            0f,
            Vector3.Dot(horizontalVelocity, forward));

        if (!_hasInertiaSample)
        {
            _smoothedLocalVelocity = localVelocity;
            _previousSmoothedLocalVelocity = localVelocity;
            _hasInertiaSample = true;
        }

        var velocitySmoothing = 1f - Mathf.Exp(-10f * deltaTime);
        _smoothedLocalVelocity = Vector3.Lerp(_smoothedLocalVelocity, localVelocity, velocitySmoothing);
        var localAcceleration = (_smoothedLocalVelocity - _previousSmoothedLocalVelocity)
            / Mathf.Max(deltaTime, 0.001f);
        _previousSmoothedLocalVelocity = _smoothedLocalVelocity;
        localAcceleration.x = Mathf.Clamp(localAcceleration.x, -18f, 18f);
        localAcceleration.z = Mathf.Clamp(localAcceleration.z, -18f, 18f);

        var strength = _settings.MovementIntensity.Value;
        var targetPosition = new Vector3(
            Mathf.Clamp(-localAcceleration.x * 0.00055f, -0.018f, 0.018f),
            0f,
            Mathf.Clamp(-localAcceleration.z * 0.00075f, -0.020f, 0.020f)) * strength;
        var targetRotation = new Vector3(
            Mathf.Clamp(localAcceleration.z * 0.055f, -2.5f, 2.5f),
            Mathf.Clamp(-localAcceleration.x * 0.022f, -3.0f, 3.0f),
            Mathf.Clamp(localAcceleration.x * 0.070f, -1.8f, 1.8f)) * strength;

        const float response = 0.12f;
        _inertiaPosition = Vector3.SmoothDamp(
            _inertiaPosition,
            targetPosition,
            ref _inertiaPositionVelocity,
            response,
            Mathf.Infinity,
            deltaTime);
        _inertiaRotation = Vector3.SmoothDamp(
            _inertiaRotation,
            targetRotation,
            ref _inertiaRotationVelocity,
            response,
            Mathf.Infinity,
            deltaTime);
    }

    private float GetAdsTarget(bool aiming)
    {
        if (!aiming)
        {
            return 0f;
        }

        if (_settings.AdsCameraMode.Value == BodycamAdsMode.Vanilla)
        {
            return 1f;
        }

        if (_settings.AdsCameraMode.Value == BodycamAdsMode.ShoulderAssist)
        {
            return 0.28f;
        }

        return 0f;
    }

    private static float ExpLerp(float current, float target, float speed, float deltaTime)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * deltaTime));
    }
}
