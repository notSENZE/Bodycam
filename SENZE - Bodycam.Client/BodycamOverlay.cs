using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.CameraControl;
using EFT.HealthSystem;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI.Screens;
using UnityEngine;

namespace SenzeBodycam;

internal sealed class BodycamOverlay
{
    private const float MinimumCensorshipScreenHeightRatio = 0.003f;
    private const float MinimumCensorshipScreenSize = 4f;
    private const float FaceForwardOffset = 0.085f;

    private static readonly FieldInfo CorpsePlayerBodyField = typeof(Corpse).GetField(
        "PlayerBody",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly Color HudColor = new Color(0.64f, 0.65f, 0.62f, 0.82f);
    private static readonly Color TextColor = new Color(0.78f, 0.79f, 0.76f, 0.91f);
    private static readonly Color ShadowColor = new Color(0.01f, 0.012f, 0.01f, 0.62f);

    private readonly ModConfig _settings;
    private readonly BodyCameraController _bodyCamera;
    private readonly HashSet<ProjectileHitKey> _projectileHits = new HashSet<ProjectileHitKey>();
    private readonly Dictionary<string, FaceMosaic> _faceMosaics = new Dictionary<string, FaceMosaic>();
    private readonly RaycastHit[] _faceOcclusionHits = new RaycastHit[24];
    private readonly LensDamageMark[] _nativeLensDamageMarks = new LensDamageMark[2];
    private readonly System.Random _hudRandom = new System.Random();

    private Texture2D _scanlineTexture;
    private Texture2D _recordingLabelTexture;
    private Texture2D _dimRecordingLabelTexture;
    private Texture2D _senzeLogoTexture;
    private Texture2D _disconnectNoiseTexture;
    private Camera _faceCaptureCamera;
    private FaceMosaicCapture _faceMosaicCapture;
    private Mesh _faceMosaicMesh;
    private Material _faceMosaicMaterial;
    private Camera _nativeVisorCamera;
    private VisorEffect _nativeVisorEffect;
    private PlayerCameraController _nativeVisorController;
    private bool _nativeVisorForced;
    private bool _nativeVisorVisible;
    private float _nativeVisorScratchIntensity;
    private float _nativeVisorDistortIntensity;
    private float _nativeVisorBlurSize;
    private int _nativeVisorDownsample;
    private int _nativeVisorBlurIterations;
    private float _nativeVisorMinLightValue;
    private bool _nativeVisorAffectedByLight;
    private RenderTexture _nativeLensDamageTexture;
    private Texture2D _nativeLensDamageDisplayTexture;
    private int _nativeLensHitCount;
    private bool _nativeLensDamageDirty;
    private GUIStyle _realBodycamPrimaryStyle;
    private GUIStyle _realBodycamSecondaryStyle;
    private GUIStyle _magazineCountStyle;
    private GUIStyle _magazineAmmoStyle;
    private GUIStyle _disconnectStyle;
    private Font _bahnschriftFont;
    private int _cachedFontSize;
    private int _cachedMagazineFontSize;
    private int _cachedRecordingTextFontSize;
    private Player _damageOwner;
    private Player _raidOwner;
    private bool _disconnected;
    private float _nextNoiseUpdate;
    private float _interferenceStartedAt = -1f;
    private float _interferenceEndsAt = -1f;
    private float _interferenceIntensity;
    private float _nextInterferenceUpdate;
    private float _interferenceNoiseAlpha;
    private float _interferenceBlackoutAlpha;
    private float _interferenceBandPosition;
    private float _interferenceBandHeight;
    private float _interferenceColorOffset;

    internal BodycamOverlay(ModConfig settings, BodyCameraController bodyCamera)
    {
        _settings = settings;
        _bodyCamera = bodyCamera;
    }

    internal bool TriggerElectricalInterference(float intensity, float duration)
    {
        if (!_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || !_settings.OverlayEnabled.Value
            || !_bodyCamera.ShouldDrawOverlay)
        {
            return false;
        }

        var now = Time.unscaledTime;
        var wasActive = now < _interferenceEndsAt;
        _interferenceStartedAt = now;
        _interferenceEndsAt = Mathf.Max(_interferenceEndsAt, now + Mathf.Clamp(duration, 0.1f, 3f));
        _interferenceIntensity = Mathf.Max(
            wasActive ? _interferenceIntensity : 0f,
            Mathf.Clamp01(intensity));
        _nextInterferenceUpdate = 0f;
        return true;
    }

    internal void Draw()
    {
        RefreshRaidContext();
        if (!_settings.OverlayEnabled.Value
            || !_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || (!_bodyCamera.ShouldDrawOverlay && !_disconnected)
            || Event.current.type != EventType.Repaint)
        {
            return;
        }

        EnsureTextures();
        EnsureDamageOwner();

        var resolutionScale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        var scale = Mathf.Max(0.4f, resolutionScale * _settings.OverlayScale.Value);
        EnsureStyles(scale);

        var previousDepth = GUI.depth;
        var previousColor = GUI.color;
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUI.depth = -1000;

        if (_disconnected)
        {
            DrawDisconnectedScreen(scale);
        }
        else
        {
            DrawLensEffects();
            DrawNativeLensDamage();
            DrawRealBodycamStamp(scale);

            DrawMagazineCheckLabel(scale);
            DrawElectricalInterference();
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
        GUI.matrix = previousMatrix;
    }

    internal void Dispose()
    {
        RestoreNativeVisorLensEffect();
        RemoveFaceMosaicCapture();
        DestroyFaceMosaicRenderer();
        DestroyTexture(_scanlineTexture);
        DestroyTexture(_dimRecordingLabelTexture);
        DestroyTexture(_recordingLabelTexture);
        DestroyTexture(_senzeLogoTexture);
        DestroyTexture(_disconnectNoiseTexture);
        if (_bahnschriftFont != null)
        {
            UnityEngine.Object.Destroy(_bahnschriftFont);
        }
        if (_nativeLensDamageTexture != null)
        {
            UnityEngine.Object.Destroy(_nativeLensDamageTexture);
        }
        DestroyTexture(_nativeLensDamageDisplayTexture);
        _scanlineTexture = null;
        _recordingLabelTexture = null;
        _dimRecordingLabelTexture = null;
        _senzeLogoTexture = null;
        _disconnectNoiseTexture = null;
        _bahnschriftFont = null;
        _nativeLensDamageTexture = null;
        _nativeLensDamageDisplayTexture = null;
        ClearFaceCensorTextures();
        _damageOwner = null;
        _raidOwner = null;
        _projectileHits.Clear();
        _interferenceStartedAt = -1f;
        _interferenceEndsAt = -1f;
        _interferenceIntensity = 0f;
        ResetNativeLensDamage();
    }

    internal void ApplyCameraEffects(PlayerCameraController cameraController)
    {
        UpdateFaceMosaicCapture();
        UpdateNativeVisorLensEffect(cameraController);
    }

    internal bool ShouldReplaceMagazineCheckDetails()
    {
        if (!_settings.MagazineCheckLabelEnabled.Value
            || !_settings.OverlayEnabled.Value
            || !_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || !_bodyCamera.ShouldDrawOverlay)
        {
            return false;
        }

        return TryGetMagazineContext(false, out _, out _, out _);
    }

    internal void AddDamage(float damage, float absorbed)
    {
        if (!CanTrackDamageEffects())
        {
            return;
        }

        EnsureDamageOwner();
        AddLensDamage();
    }

    internal void AddProjectileDamage(DamageInfo damageInfo, ShotId shotId)
    {
        if (!CanTrackDamageEffects())
        {
            return;
        }

        EnsureDamageOwner();
        var hitKey = new ProjectileHitKey(
            shotId._ammoId,
            damageInfo.FireIndex,
            shotId._fragmentIndex);
        if (!_projectileHits.Add(hitKey))
        {
            return;
        }

        AddLensDamage();
    }

    private bool CanTrackDamageEffects()
    {
        return _settings.ModEnabled.Value
            && _settings.BodyCameraEnabled.Value
            && _settings.OverlayEnabled.Value
            && _settings.DamageEffectsEnabled.Value
            && _bodyCamera.ShouldDrawOverlay;
    }

    private void AddLensDamage()
    {
        if (_nativeLensHitCount >= 2)
        {
            return;
        }

        _nativeLensDamageMarks[_nativeLensHitCount] = CreateLensDamageMark();
        _nativeLensHitCount++;
        _nativeLensDamageDirty = true;
    }

    private LensDamageMark CreateLensDamageMark()
    {
        var position = RandomLensDamagePosition();
        if (_nativeLensHitCount > 0)
        {
            var firstPosition = _nativeLensDamageMarks[0].Position;
            var bestPosition = position;
            var bestDistance = (position - firstPosition).sqrMagnitude;
            for (var attempt = 0; attempt < 10 && bestDistance < 0.36f; attempt++)
            {
                position = RandomLensDamagePosition();
                var distance = (position - firstPosition).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestPosition = position;
                    bestDistance = distance;
                }
            }
            position = bestPosition;
        }

        return new LensDamageMark(
            position,
            Mathf.Lerp(1.15f, 1.75f, (float)_hudRandom.NextDouble()),
            Mathf.Lerp(0f, 360f, (float)_hudRandom.NextDouble()));
    }

    private Vector2 RandomLensDamagePosition()
    {
        var position = Vector2.zero;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            position = new Vector2(
                Mathf.Lerp(-0.88f, 0.88f, (float)_hudRandom.NextDouble()),
                Mathf.Lerp(-0.78f, 0.78f, (float)_hudRandom.NextDouble()));
            if (Mathf.Abs(position.x) >= 0.5f || Mathf.Abs(position.y) >= 0.42f)
            {
                return position;
            }
        }

        position.x = position.x < 0f ? -0.55f : 0.55f;
        return position;
    }

    private void DrawLensEffects()
    {
        var previousColor = GUI.color;
        var noiseStrength = Mathf.Clamp01(_settings.SensorNoiseStrength.Value);
        GUI.color = new Color(1f, 1f, 1f, noiseStrength * 0.02f);
        var scanlineOffset = Mathf.Repeat(Time.unscaledTime * 7f, 1f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _scanlineTexture,
            new Rect(0f, scanlineOffset, 1f, Screen.height / 4f));
        GUI.color = previousColor;
    }

    private void DrawElectricalInterference()
    {
        var now = Time.unscaledTime;
        if (_interferenceStartedAt < 0f || now >= _interferenceEndsAt)
        {
            _interferenceStartedAt = -1f;
            _interferenceEndsAt = -1f;
            _interferenceIntensity = 0f;
            return;
        }

        var totalDuration = Mathf.Max(0.1f, _interferenceEndsAt - _interferenceStartedAt);
        var elapsed = now - _interferenceStartedAt;
        var remaining = _interferenceEndsAt - now;
        var attack = Mathf.Clamp01(elapsed / 0.045f);
        var fade = Mathf.Clamp01(remaining / Mathf.Max(0.12f, totalDuration * 0.38f));
        var strength = _interferenceIntensity * attack * fade;

        if (now >= _nextInterferenceUpdate)
        {
            _nextInterferenceUpdate = now + 0.035f + (float)_hudRandom.NextDouble() * 0.045f;
            _interferenceNoiseAlpha = Mathf.Lerp(0.11f, 0.34f, (float)_hudRandom.NextDouble());
            _interferenceBlackoutAlpha = _hudRandom.NextDouble() < 0.16d * strength
                ? Mathf.Lerp(0.28f, 0.72f, (float)_hudRandom.NextDouble())
                : Mathf.Lerp(0.015f, 0.08f, (float)_hudRandom.NextDouble());
            _interferenceBandPosition = (float)_hudRandom.NextDouble();
            _interferenceBandHeight = Mathf.Lerp(0.018f, 0.12f, (float)_hudRandom.NextDouble());
            _interferenceColorOffset = Mathf.Lerp(2f, 9f, (float)_hudRandom.NextDouble());
        }

        UpdateDisconnectNoise();
        var previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, _interferenceNoiseAlpha * strength);
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _disconnectNoiseTexture,
            ScaleMode.StretchToFill,
            false);

        DrawRect(
            new Rect(0f, 0f, Screen.width, Screen.height),
            new Color(0f, 0f, 0f, _interferenceBlackoutAlpha * strength));

        var bandY = Screen.height * _interferenceBandPosition;
        var bandHeight = Mathf.Max(2f, Screen.height * _interferenceBandHeight);
        DrawRect(
            new Rect(0f, bandY, Screen.width, bandHeight),
            new Color(0.005f, 0.008f, 0.009f, 0.48f * strength));
        DrawRect(
            new Rect(_interferenceColorOffset, bandY - 2f, Screen.width, 2f),
            new Color(0.05f, 0.72f, 0.78f, 0.34f * strength));
        DrawRect(
            new Rect(-_interferenceColorOffset, bandY + bandHeight, Screen.width, 2f),
            new Color(0.82f, 0.06f, 0.21f, 0.28f * strength));

        GUI.color = new Color(1f, 1f, 1f, 0.11f * strength);
        var scanlineOffset = Mathf.Repeat(now * 38f, 1f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _scanlineTexture,
            new Rect(0f, scanlineOffset, 1f, Screen.height / 2f));
        GUI.color = previousColor;
    }

    internal void CaptureFaceMosaics(Camera camera)
    {
        if (!_settings.FaceCensorEnabled.Value
            || !_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || !_bodyCamera.ShouldDrawOverlay
            || !ReferenceEquals(camera, _bodyCamera.OverlayCamera)
            || !Singleton<GameWorld>.Instantiated
            || !IsCensorshipViewActive())
        {
            ClearFaceCensorTextures();
            return;
        }

        var gameWorld = Singleton<GameWorld>.Instance;
        var localPlayer = _bodyCamera.OverlayPlayer;
        if (gameWorld == null || localPlayer == null)
        {
            return;
        }

        var sizeScale = _settings.FaceCensorSize.Value;
        var corpses = gameWorld.LootList
            .OfType<Corpse>()
            .Where(corpse => corpse != null && corpse.gameObject.activeInHierarchy)
            .ToArray();
        var corpseProfileIds = new HashSet<string>(
            corpses
                .Select(corpse => corpse.PlayerProfileID)
                .Where(profileId => !string.IsNullOrEmpty(profileId)));

        foreach (var registeredPlayer in gameWorld.RegisteredPlayers)
        {
            var player = registeredPlayer as Player;
            if (player == null
                || ReferenceEquals(player, localPlayer)
                || player.PlayerBones?.Head == null
                || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            var playerIsAlive = player.HealthController != null
                && player.HealthController.IsAlive;
            if (!playerIsAlive
                && !string.IsNullOrEmpty(player.ProfileId)
                && corpseProfileIds.Contains(player.ProfileId))
            {
                continue;
            }

            var head = player.PlayerBones.Head.Original;
            var playerRoot = player.Transform?.Original;
            if (head == null || playerRoot == null)
            {
                continue;
            }

            CaptureFaceMosaic(
                string.IsNullOrEmpty(player.ProfileId)
                    ? $"player:{player.GetInstanceID()}"
                    : player.ProfileId,
                head,
                playerRoot,
                player,
                gameWorld,
                localPlayer,
                camera,
                sizeScale);
        }

        foreach (var corpse in corpses)
        {
            var playerBody = CorpsePlayerBodyField?.GetValue(corpse) as PlayerBody
                ?? corpse.GetComponentInChildren<PlayerBody>();
            var head = playerBody?.PlayerBones?.Head?.Original;
            if (head == null)
            {
                continue;
            }

            CaptureFaceMosaic(
                string.IsNullOrEmpty(corpse.PlayerProfileID)
                    ? $"corpse:{corpse.GetInstanceID()}"
                    : corpse.PlayerProfileID,
                head,
                corpse.transform,
                null,
                gameWorld,
                localPlayer,
                camera,
                sizeScale);
        }

        RemoveStaleFaceMosaics();
    }

    private void CaptureFaceMosaic(
        string targetId,
        Transform head,
        Transform targetRoot,
        Player targetPlayer,
        GameWorld gameWorld,
        Player localPlayer,
        Camera camera,
        float sizeScale)
    {
        if (!_faceMosaics.TryGetValue(targetId, out var mosaic))
        {
            mosaic = new FaceMosaic();
            _faceMosaics[targetId] = mosaic;
        }

        var targetIsAlive = targetPlayer?.HealthController != null
            && targetPlayer.HealthController.IsAlive;
        var referenceDirection = targetIsAlive ? targetPlayer.LookDirection : targetRoot.forward;
        if (referenceDirection.sqrMagnitude < 0.0001f)
        {
            referenceDirection = targetRoot.forward;
        }

        if (targetIsAlive || !mosaic.HasHeadLocalFaceDirection)
        {
            mosaic.HeadLocalFaceDirection = head.InverseTransformDirection(referenceDirection.normalized);
            mosaic.HasHeadLocalFaceDirection = mosaic.HeadLocalFaceDirection.sqrMagnitude > 0.0001f;
            mosaic.HeadLocalUpDirection = head.InverseTransformDirection(targetRoot.up.normalized);
            mosaic.HasHeadLocalUpDirection = mosaic.HeadLocalUpDirection.sqrMagnitude > 0.0001f;
        }

        var faceDirection = mosaic.HasHeadLocalFaceDirection
            ? head.TransformDirection(mosaic.HeadLocalFaceDirection).normalized
            : referenceDirection.normalized;
        var faceUp = mosaic.HasHeadLocalUpDirection
            ? head.TransformDirection(mosaic.HeadLocalUpDirection).normalized
            : targetRoot.up.normalized;

        var faceCentre = head.position + faceDirection.normalized * FaceForwardOffset;
        var eyeMosaic = _settings.CensorshipStyle.Value == CensorshipMode.EyeMosaicBar;
        var censorshipCentre = eyeMosaic
            ? faceCentre + faceUp * _settings.EyeMosaicVerticalOffset.Value
            : faceCentre;
        var worldWidth = eyeMosaic
            ? 0.235f * sizeScale * _settings.EyeMosaicWidth.Value
            : 0.27f * sizeScale;
        var worldHeight = eyeMosaic
            ? 0.064f * sizeScale * _settings.EyeMosaicHeight.Value
            : 0.27f * sizeScale;
        var cameraDistance = Vector3.Distance(censorshipCentre, camera.transform.position);
        var maximumDistance = _settings.CensorshipMaximumDistance.Value;
        if (cameraDistance > maximumDistance
            || !TryGetCensorshipRect(camera, censorshipCentre, worldWidth, worldHeight, out var faceRect)
            || !IsFaceVisible(gameWorld, localPlayer, targetPlayer, targetRoot, censorshipCentre))
        {
            return;
        }

        mosaic.ScreenRect = faceRect;
        mosaic.WorldCentre = censorshipCentre;
        mosaic.WorldWidth = worldWidth;
        mosaic.WorldHeight = worldHeight;
        mosaic.WorldUp = faceUp;
        mosaic.LastVisibleFrame = Time.frameCount;
        if (Time.unscaledTime < mosaic.NextCaptureTime)
        {
            return;
        }

        var distanceRatio = Mathf.InverseLerp(10f, maximumDistance, cameraDistance);
        mosaic.NextCaptureTime = Time.unscaledTime + Mathf.Lerp(0.06f, 0.22f, distanceRatio);
        CaptureMosaicTexture(mosaic, faceRect, cameraDistance);
    }

    internal void RenderFaceMosaics(Camera camera)
    {
        if (!_settings.FaceCensorEnabled.Value
            || !_settings.ModEnabled.Value
            || !_settings.BodyCameraEnabled.Value
            || !_bodyCamera.ShouldDrawOverlay
            || !ReferenceEquals(camera, _bodyCamera.OverlayCamera)
            || !IsCensorshipViewActive()
            || !EnsureFaceMosaicRenderer())
        {
            return;
        }

        foreach (var mosaic in _faceMosaics.Values)
        {
            if (mosaic.Texture == null || Time.frameCount - mosaic.LastVisibleFrame > 2)
            {
                continue;
            }

            var directionToCamera = camera.transform.position - mosaic.WorldCentre;
            if (directionToCamera.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            var faceDepthClearance = Mathf.Max(0.06f, Mathf.Max(mosaic.WorldWidth, mosaic.WorldHeight) * 0.28f);
            var renderPosition = mosaic.WorldCentre + directionToCamera.normalized * faceDepthClearance;
            var renderUp = Vector3.ProjectOnPlane(mosaic.WorldUp, directionToCamera);
            if (renderUp.sqrMagnitude < 0.0001f)
            {
                renderUp = camera.transform.up;
            }
            var renderRotation = Quaternion.LookRotation(directionToCamera, renderUp.normalized);
            var renderScale = new Vector3(mosaic.WorldWidth, mosaic.WorldHeight, 1f);
            _faceMosaicMaterial.mainTexture = mosaic.Texture;
            if (_faceMosaicMaterial.SetPass(0))
            {
                Graphics.DrawMeshNow(
                    _faceMosaicMesh,
                    Matrix4x4.TRS(renderPosition, renderRotation, renderScale));
            }
        }
    }

    private bool EnsureFaceMosaicRenderer()
    {
        if (_faceMosaicMesh == null)
        {
            _faceMosaicMesh = new Mesh
            {
                name = "Senze Bodycam Face Mosaic Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _faceMosaicMesh.RecalculateBounds();
        }

        if (_faceMosaicMaterial != null)
        {
            return true;
        }

        var shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            return false;
        }

        _faceMosaicMaterial = new Material(shader)
        {
            name = "Senze Bodycam Face Mosaic Material",
            color = Color.white,
            hideFlags = HideFlags.HideAndDontSave
        };
        return true;
    }

    private void DestroyFaceMosaicRenderer()
    {
        if (_faceMosaicMaterial != null)
        {
            UnityEngine.Object.Destroy(_faceMosaicMaterial);
        }
        if (_faceMosaicMesh != null)
        {
            UnityEngine.Object.Destroy(_faceMosaicMesh);
        }
        _faceMosaicMaterial = null;
        _faceMosaicMesh = null;
    }

    private static bool TryGetCensorshipRect(
        Camera camera,
        Vector3 worldCentre,
        float worldWidth,
        float worldHeight,
        out Rect rect)
    {
        rect = default;
        var screenCentre = camera.WorldToScreenPoint(worldCentre);
        if (screenCentre.z <= 0.05f
            || screenCentre.x < -100f
            || screenCentre.x > Screen.width + 100f
            || screenCentre.y < -100f
            || screenCentre.y > Screen.height + 100f)
        {
            return false;
        }

        var cameraTransform = camera.transform;
        var screenRight = camera.WorldToScreenPoint(worldCentre + cameraTransform.right * worldWidth * 0.5f);
        var screenTop = camera.WorldToScreenPoint(worldCentre + cameraTransform.up * worldHeight * 0.5f);
        var width = Mathf.Abs(screenRight.x - screenCentre.x) * 2f;
        var height = Mathf.Abs(screenTop.y - screenCentre.y) * 2f;
        var minimumFaceSize = Mathf.Max(
            MinimumCensorshipScreenSize,
            Screen.height * MinimumCensorshipScreenHeightRatio);
        if (width < minimumFaceSize || height < 1f)
        {
            return false;
        }

        var scale = Mathf.Min(
            1f,
            Screen.width * 0.34f / width,
            Screen.height * 0.46f / height);
        width = Mathf.Max(2f, width * scale);
        height = Mathf.Max(2f, height * scale);
        rect = new Rect(
            screenCentre.x - width * 0.5f,
            Screen.height - screenCentre.y - height * 0.5f,
            width,
            height);
        return true;
    }

    private static bool IsCensorshipViewActive()
    {
        return EftScreenManager.Instance.CheckCurrentScreen(EEftScreenType.BattleUI);
    }

    private void CaptureMosaicTexture(FaceMosaic mosaic, Rect screenRect, float cameraDistance)
    {
        var left = Mathf.Clamp(Mathf.FloorToInt(screenRect.x), 0, Screen.width - 1);
        var top = Mathf.Clamp(Mathf.FloorToInt(screenRect.y), 0, Screen.height - 1);
        var right = Mathf.Clamp(Mathf.CeilToInt(screenRect.xMax), left + 1, Screen.width);
        var bottom = Mathf.Clamp(Mathf.CeilToInt(screenRect.yMax), top + 1, Screen.height);
        var width = right - left;
        var height = bottom - top;
        if (width < 2 || height < 2)
        {
            return;
        }

        if (mosaic.Capture == null || mosaic.Capture.width != width || mosaic.Capture.height != height)
        {
            DestroyTexture(mosaic.Capture);
            mosaic.Capture = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                name = "Senze Bodycam Face Capture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        mosaic.Capture.ReadPixels(new Rect(left, Screen.height - bottom, width, height), 0, 0, false);
        mosaic.Capture.Apply(false, false);

        var configuredBlockCount = Mathf.Clamp(Mathf.RoundToInt(_settings.CensorshipBlockCount.Value), 5, 18);
        var distantBlockCount = Mathf.Max(5, Mathf.RoundToInt(configuredBlockCount * 0.62f));
        var distanceRatio = Mathf.InverseLerp(
            3f,
            _settings.CensorshipMaximumDistance.Value,
            cameraDistance);
        var mosaicWidth = Mathf.RoundToInt(Mathf.Lerp(configuredBlockCount, distantBlockCount, distanceRatio));
        var mosaicHeight = Mathf.Max(2, Mathf.RoundToInt(mosaicWidth * height / (float)width));
        if (mosaic.Texture == null
            || mosaic.Texture.width != mosaicWidth
            || mosaic.Texture.height != mosaicHeight)
        {
            DestroyTexture(mosaic.Texture);
            mosaic.Texture = new Texture2D(mosaicWidth, mosaicHeight, TextureFormat.RGB24, false)
            {
                name = "Senze Bodycam Privacy Mosaic",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var sourcePixels = mosaic.Capture.GetPixels32();
        var mosaicPixels = new Color32[mosaicWidth * mosaicHeight];
        for (var mosaicY = 0; mosaicY < mosaicHeight; mosaicY++)
        {
            var sourceYStart = Mathf.FloorToInt(mosaicY * height / (float)mosaicHeight);
            var sourceYEnd = Mathf.Max(sourceYStart + 1, Mathf.CeilToInt((mosaicY + 1) * height / (float)mosaicHeight));
            for (var mosaicX = 0; mosaicX < mosaicWidth; mosaicX++)
            {
                var sourceXStart = Mathf.FloorToInt(mosaicX * width / (float)mosaicWidth);
                var sourceXEnd = Mathf.Max(sourceXStart + 1, Mathf.CeilToInt((mosaicX + 1) * width / (float)mosaicWidth));
                long red = 0;
                long green = 0;
                long blue = 0;
                var sampleCount = 0;
                for (var sourceY = sourceYStart; sourceY < sourceYEnd && sourceY < height; sourceY++)
                {
                    for (var sourceX = sourceXStart; sourceX < sourceXEnd && sourceX < width; sourceX++)
                    {
                        var source = sourcePixels[sourceY * width + sourceX];
                        red += source.r;
                        green += source.g;
                        blue += source.b;
                        sampleCount++;
                    }
                }

                mosaicPixels[mosaicY * mosaicWidth + mosaicX] = new Color32(
                    (byte)(red / sampleCount),
                    (byte)(green / sampleCount),
                    (byte)(blue / sampleCount),
                    byte.MaxValue);
            }
        }

        mosaic.Texture.SetPixels32(mosaicPixels);
        mosaic.Texture.Apply(false, false);
    }

    private bool IsFaceVisible(
        GameWorld gameWorld,
        Player localPlayer,
        Player targetPlayer,
        Transform targetRoot,
        Vector3 faceCentre)
    {
        var origin = _bodyCamera.OverlayCamera.transform.position;
        var direction = faceCentre - origin;
        var distance = direction.magnitude;
        if (distance <= 0.05f)
        {
            return true;
        }

        var hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            _faceOcclusionHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        var nearestTarget = distance;
        var nearestBlocker = float.PositiveInfinity;
        for (var index = 0; index < hitCount; index++)
        {
            var hit = _faceOcclusionHits[index];
            if (hit.collider == null)
            {
                continue;
            }

            var hitPlayer = gameWorld.GetPlayerByCollider(hit.collider);
            if (ReferenceEquals(hitPlayer, localPlayer)
                || ColliderBelongsToPlayer(hit.collider, localPlayer))
            {
                continue;
            }

            if (ReferenceEquals(hitPlayer, targetPlayer)
                || ColliderBelongsToRoot(hit.collider, targetRoot))
            {
                nearestTarget = Mathf.Min(nearestTarget, hit.distance);
            }
            else
            {
                nearestBlocker = Mathf.Min(nearestBlocker, hit.distance);
            }
        }

        return nearestBlocker + 0.08f >= nearestTarget;
    }

    private static bool ColliderBelongsToPlayer(Collider collider, Player player)
    {
        return ColliderBelongsToRoot(collider, player?.Transform?.Original);
    }

    private static bool ColliderBelongsToRoot(Collider collider, Transform root)
    {
        var colliderTransform = collider?.transform;
        return root != null
            && colliderTransform != null
            && (ReferenceEquals(colliderTransform, root)
                || colliderTransform.IsChildOf(root));
    }

    private void RemoveStaleFaceMosaics()
    {
        var staleIds = _faceMosaics
            .Where(pair => Time.frameCount - pair.Value.LastVisibleFrame > 180)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var staleId in staleIds)
        {
            DestroyFaceMosaic(_faceMosaics[staleId]);
            _faceMosaics.Remove(staleId);
        }
    }

    private void ClearFaceCensorTextures()
    {
        foreach (var mosaic in _faceMosaics.Values)
        {
            DestroyFaceMosaic(mosaic);
        }
        _faceMosaics.Clear();
    }

    private static void DestroyFaceMosaic(FaceMosaic mosaic)
    {
        DestroyTexture(mosaic.Capture);
        DestroyTexture(mosaic.Texture);
    }

    private void UpdateFaceMosaicCapture()
    {
        var camera = _bodyCamera.OverlayCamera;
        if (!ReferenceEquals(_faceCaptureCamera, camera))
        {
            RemoveFaceMosaicCapture();
            _faceCaptureCamera = camera;
        }

        var shouldAttach = camera != null
            && _settings.ModEnabled.Value
            && _settings.BodyCameraEnabled.Value
            && _bodyCamera.ShouldDrawOverlay
            && (_settings.FaceCensorEnabled.Value || _settings.LensSmearStrength.Value > 0.001f);
        if (!shouldAttach)
        {
            if (_faceMosaicCapture != null)
            {
                _faceMosaicCapture.enabled = false;
            }
            return;
        }

        if (_faceMosaicCapture == null)
        {
            _faceMosaicCapture = camera.gameObject.AddComponent<FaceMosaicCapture>();
        }
        _faceMosaicCapture.enabled = true;
    }

    private void RemoveFaceMosaicCapture()
    {
        if (_faceMosaicCapture != null)
        {
            UnityEngine.Object.Destroy(_faceMosaicCapture);
        }
        _faceMosaicCapture = null;
        _faceCaptureCamera = null;
    }

    private void UpdateNativeVisorLensEffect(PlayerCameraController cameraController)
    {
        var camera = _bodyCamera.OverlayCamera;
        if (!ReferenceEquals(_nativeVisorCamera, camera)
            || !ReferenceEquals(_nativeVisorController, cameraController))
        {
            RestoreNativeVisorLensEffect();
            _nativeVisorCamera = camera;
            _nativeVisorController = cameraController;
            _nativeVisorEffect = camera != null ? camera.GetComponent<VisorEffect>() : null;
            _nativeLensDamageDirty = true;
        }

        UpdateNativeLensDamageTexture();
        var smearStrength = Mathf.Clamp01(_settings.LensSmearStrength.Value);
        var shouldUseBodycamLens = _nativeVisorEffect != null
            && _settings.ModEnabled.Value
            && _settings.BodyCameraEnabled.Value
            && _bodyCamera.ShouldDrawOverlay
            && _settings.OverlayEnabled.Value;
        if (!shouldUseBodycamLens)
        {
            RestoreNativeVisorLensEffect();
            return;
        }

        if (!_nativeVisorForced)
        {
            _nativeVisorVisible = _nativeVisorEffect.Visible;
            _nativeVisorScratchIntensity = _nativeVisorEffect.ScratcesIntensity;
            _nativeVisorDistortIntensity = _nativeVisorEffect.DistortIntensity;
            _nativeVisorBlurSize = _nativeVisorEffect.blurSize;
            _nativeVisorDownsample = _nativeVisorEffect.downsample;
            _nativeVisorBlurIterations = _nativeVisorEffect.blurIterations;
            _nativeVisorMinLightValue = _nativeVisorEffect.MinLightValue;
            _nativeVisorAffectedByLight = _nativeVisorEffect.IsAffectedByLight;
            _nativeVisorForced = true;
        }

        ApplyPersistentLensSmear(smearStrength);
    }

    internal void PrepareNativeVisorForRender(Camera camera)
    {
        if (_nativeVisorForced && ReferenceEquals(camera, _nativeVisorCamera))
        {
            ApplyPersistentLensSmear(Mathf.Clamp01(_settings.LensSmearStrength.Value));
        }
    }

    private void ApplyPersistentLensSmear(float visibility)
    {
        _nativeVisorEffect.Visible = true;
        _nativeVisorEffect.SetMask(VisorEffect.EMask.NoMask);
        _nativeVisorEffect.ScratcesIntensity = visibility;
        _nativeVisorEffect.DistortIntensity = 0f;
        _nativeVisorEffect.blurSize = 0f;
        _nativeVisorEffect.downsample = 0;
        _nativeVisorEffect.blurIterations = 0;
        _nativeVisorEffect.MinLightValue = 0f;
        _nativeVisorEffect.IsAffectedByLight = false;
        _nativeVisorEffect.SetIntensity(1f);
    }

    private void RestoreNativeVisorLensEffect()
    {
        if (_nativeVisorEffect == null || !_nativeVisorForced)
        {
            return;
        }

        _nativeVisorEffect.ScratcesIntensity = _nativeVisorScratchIntensity;
        _nativeVisorEffect.DistortIntensity = _nativeVisorDistortIntensity;
        _nativeVisorEffect.blurSize = _nativeVisorBlurSize;
        _nativeVisorEffect.downsample = _nativeVisorDownsample;
        _nativeVisorEffect.blurIterations = _nativeVisorBlurIterations;
        _nativeVisorEffect.MinLightValue = _nativeVisorMinLightValue;
        _nativeVisorEffect.IsAffectedByLight = _nativeVisorAffectedByLight;
        _nativeVisorEffect.Visible = _nativeVisorVisible;

        var player = _bodyCamera.OverlayPlayer;
        var faceShield = player?.FaceCoverObserver?.Component
            ?? player?.FaceShieldObserver?.Component;
        _nativeVisorController?.OnFaceShieldComponentChanged(faceShield);

        _nativeVisorForced = false;
    }

    private void UpdateNativeLensDamageTexture()
    {
        if (_nativeVisorEffect == null || _nativeVisorCamera == null)
        {
            return;
        }

        if (_nativeLensDamageTexture == null
            || _nativeLensDamageTexture.width != Screen.width
            || _nativeLensDamageTexture.height != Screen.height)
        {
            if (_nativeLensDamageTexture != null)
            {
                UnityEngine.Object.Destroy(_nativeLensDamageTexture);
            }

            _nativeLensDamageTexture = new RenderTexture(
                Screen.width,
                Screen.height,
                0,
                RenderTextureFormat.R8,
                RenderTextureReadWrite.Default)
            {
                name = "Senze Bodycam Lens Damage",
                filterMode = FilterMode.Bilinear,
                autoGenerateMips = false,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            DestroyTexture(_nativeLensDamageDisplayTexture);
            _nativeLensDamageDisplayTexture = new Texture2D(
                Screen.width,
                Screen.height,
                TextureFormat.RGBA32,
                false)
            {
                name = "Senze Bodycam Neutral Lens Damage",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _nativeLensDamageDirty = true;
        }

        if (!_nativeLensDamageDirty)
        {
            return;
        }

        var previousTarget = RenderTexture.active;
        Graphics.SetRenderTarget(_nativeLensDamageTexture);
        GL.Clear(true, true, Color.clear);
        for (var index = 0; index < _nativeLensHitCount; index++)
        {
            var mark = _nativeLensDamageMarks[index];
            var position = new Vector3(mark.Position.x, mark.Position.y);
            var scale = new Vector3(mark.Size / _nativeVisorCamera.aspect, mark.Size);
            _nativeVisorEffect.Draw(position, scale, mark.Rotation);
        }

        _nativeLensDamageDisplayTexture.ReadPixels(
            new Rect(0f, 0f, _nativeLensDamageTexture.width, _nativeLensDamageTexture.height),
            0,
            0,
            false);
        var damagePixels = _nativeLensDamageDisplayTexture.GetPixels32();
        for (var index = 0; index < damagePixels.Length; index++)
        {
            var mask = damagePixels[index].r;
            damagePixels[index] = new Color32(205, 208, 202, mask);
        }
        _nativeLensDamageDisplayTexture.SetPixels32(damagePixels);
        _nativeLensDamageDisplayTexture.Apply(false, false);

        RenderTexture.active = previousTarget;
        _nativeLensDamageDirty = false;
    }

    private void DrawNativeLensDamage()
    {
        var visibility = Mathf.Clamp01(_settings.LensDamageStrength.Value);
        if (!_settings.DamageEffectsEnabled.Value
            || _nativeLensHitCount <= 0
            || _nativeLensDamageDisplayTexture == null
            || visibility <= 0.001f)
        {
            return;
        }

        var previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, visibility);
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _nativeLensDamageDisplayTexture,
            ScaleMode.StretchToFill,
            true);
        GUI.color = previousColor;
    }

    private void DrawRealBodycamStamp(float scale)
    {
        var operatorName = _raidOwner?.Profile?.Info?.Nickname;
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            operatorName = "OPERATOR";
        }

        var raidTime = GetCurrentRaidWorldTime();
        var displayedDateTime = raidTime.HasValue
            ? raidTime.Value.ToString("yyyy-MM-dd    HH:mm:ss", CultureInfo.InvariantCulture)
            : "---- -- --    --:--:--";
        var lineOne = operatorName.ToUpperInvariant() + "    " + displayedDateTime;
        const string lineTwo = "STS    BODY    4    SNZ: 20231402";

        var top = 28f * scale;
        var recordingScale = scale * _settings.RecordingLabelScale.Value;
        var recordingWidth = 210f * recordingScale;
        var recordingHeight = 54f * recordingScale;
        var recordingLeft = 52f * scale;
        var recordingTop = top;
        var recordingOffsetX = _settings.RecordingLabelOffsetX.Value;
        if (recordingOffsetX >= 0f)
        {
            recordingLeft = Mathf.Lerp(
                recordingLeft,
                Screen.width - 12f * scale - recordingWidth,
                recordingOffsetX * 0.01f);
        }
        else
        {
            recordingLeft += recordingOffsetX * Screen.width * 0.01f;
        }
        var recordingOffsetY = _settings.RecordingLabelOffsetY.Value;
        if (recordingOffsetY >= 0f)
        {
            recordingTop = Mathf.Lerp(
                recordingTop,
                Screen.height - 12f * scale - recordingHeight,
                recordingOffsetY * 0.01f);
        }
        else
        {
            recordingTop += recordingOffsetY * Screen.height * 0.01f;
        }
        var recordingTexture = Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0
            ? _recordingLabelTexture
            : _dimRecordingLabelTexture;
        if (recordingTexture != null)
        {
            Graphics.DrawTexture(
                new Rect(recordingLeft, recordingTop, recordingWidth, recordingHeight),
                recordingTexture);
        }

        var logoSize = 82f * scale * _settings.SenzeLogoScale.Value;
        var rightMargin = 52f * scale;
        var logoRect = new Rect(
            Screen.width - rightMargin - logoSize + _settings.SenzeLogoOffsetX.Value * Screen.width * 0.01f,
            top - 16f * scale + _settings.SenzeLogoOffsetY.Value * Screen.height * 0.01f,
            logoSize,
            logoSize);
        GUI.color = Color.white;
        if (_senzeLogoTexture != null)
        {
            GUI.DrawTexture(logoRect, _senzeLogoTexture, ScaleMode.ScaleToFit, true);
        }

        var textScale = scale * _settings.RecordingTextScale.Value;
        var textWidth = Mathf.Max(
            _realBodycamPrimaryStyle.CalcSize(new GUIContent(lineOne)).x,
            _realBodycamSecondaryStyle.CalcSize(new GUIContent(lineTwo)).x) + 4f * scale;
        var defaultTextRight = Screen.width - rightMargin - 100f * scale;
        var textOffsetX = _settings.RecordingTextOffsetX.Value;
        var textRight = defaultTextRight;
        if (textOffsetX < 0f)
        {
            textRight = Mathf.Lerp(defaultTextRight, 12f * scale + textWidth, -textOffsetX * 0.01f);
        }
        else
        {
            textRight += textOffsetX * Screen.width * 0.01f;
        }
        var textLeft = textRight - textWidth;
        var textTop = top + _settings.RecordingTextOffsetY.Value * Screen.height * 0.01f;
        var lineHeight = 31f * textScale;
        GUI.Label(
            new Rect(textLeft, textTop, textWidth, lineHeight),
            lineOne,
            _realBodycamPrimaryStyle);
        GUI.Label(
            new Rect(textLeft, textTop + lineHeight, textWidth, lineHeight),
            lineTwo,
            _realBodycamSecondaryStyle);
    }

    private void DrawMagazineCheckLabel(float scale)
    {
        if (!_settings.MagazineCheckLabelEnabled.Value
            || !TryGetMagazineContext(true, out var magazine, out var magazineView, out var camera))
        {
            return;
        }

        var worldPosition = GetMagazineWorldPosition(magazineView);
        var screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0.01f)
        {
            return;
        }

        var firstAmmo = magazine.FirstRealAmmo() as Ammo;
        var ammoName = firstAmmo != null ? firstAmmo.Name.Localized() : "NO CARTRIDGE";
        if (string.IsNullOrWhiteSpace(ammoName))
        {
            ammoName = "UNKNOWN AMMUNITION";
        }

        var countText = $"{magazine.Count} / {magazine.MaxCount} ROUNDS";
        var windowScale = scale * Mathf.Clamp(_settings.MagazineCheckLabelScale.Value, 0.6f, 1.8f);
        var countSize = _magazineCountStyle.CalcSize(new GUIContent(countText));
        var ammoSize = _magazineAmmoStyle.CalcSize(new GUIContent(ammoName));
        var width = Mathf.Clamp(Mathf.Max(countSize.x, ammoSize.x) + 24f * windowScale, 150f * windowScale, 380f * windowScale);
        var height = 58f * windowScale;
        var left = screenPosition.x + _settings.MagazineCheckLabelOffsetX.Value * scale;
        var top = Screen.height - screenPosition.y + _settings.MagazineCheckLabelOffsetY.Value * scale;
        left = Mathf.Clamp(left, 8f * scale, Screen.width - width - 8f * scale);
        top = Mathf.Clamp(top, 8f * scale, Screen.height - height - 8f * scale);

        var panel = new Rect(left, top, width, height);
        DrawRect(new Rect(panel.x + 3f * windowScale, panel.y + 3f * windowScale, panel.width, panel.height), ShadowColor);
        DrawRect(panel, new Color(0.015f, 0.018f, 0.016f, Mathf.Clamp01(_settings.MagazineCheckBackgroundOpacity.Value)));

        var border = Mathf.Max(1f, windowScale);
        var borderColor = new Color(HudColor.r, HudColor.g, HudColor.b, 0.46f);
        DrawRect(new Rect(panel.x, panel.y, panel.width, border), borderColor);
        DrawRect(new Rect(panel.x, panel.yMax - border, panel.width, border), borderColor);
        DrawRect(new Rect(panel.x, panel.y, border, panel.height), borderColor);
        DrawRect(new Rect(panel.xMax - border, panel.y, border, panel.height), borderColor);

        var textLeft = panel.x + 12f * windowScale;
        DrawShadowedLabel(
            new Rect(textLeft, panel.y + 5f * windowScale, panel.width - 24f * windowScale, 25f * windowScale),
            countText,
            _magazineCountStyle);
        DrawShadowedLabel(
            new Rect(textLeft, panel.y + 29f * windowScale, panel.width - 24f * windowScale, 22f * windowScale),
            ammoName,
            _magazineAmmoStyle);
    }

    private bool TryGetMagazineContext(
        bool requireMagazineCheck,
        out Magazine magazine,
        out Transform magazineView,
        out Camera camera)
    {
        magazine = null;
        magazineView = null;
        camera = _bodyCamera.OverlayCamera;

        var firearmController = _bodyCamera.OverlayPlayer?.HandsController as Player.FirearmController;
        if (firearmController == null || firearmController.Weapon == null || firearmController.CCV == null || camera == null)
        {
            return false;
        }

        if (requireMagazineCheck)
        {
            var utilityOperation = firearmController.CurrentOperation as Player.FirearmController.UtilityOperation;
            if (utilityOperation == null
                || utilityOperation._utilityOperationType
                    != Player.FirearmController.UtilityOperation.EUtilityType.CheckMagazine)
            {
                return false;
            }
        }

        magazine = firearmController.Weapon.GetCurrentMagazine();
        var magazineSlot = firearmController.Weapon.GetMagazineSlot();
        if (magazine == null || magazineSlot == null)
        {
            return false;
        }

        var slotView = firearmController.CCV.GetViewForSlot(magazineSlot);
        magazineView = slotView?.ItemView;
        return magazineView != null;
    }

    private Vector3 GetMagazineWorldPosition(Transform magazineView)
    {
        var magazinePosition = magazineView.position;
        var leftPalm = _raidOwner?.PlayerBones?.LeftPalm;
        if (leftPalm == null)
        {
            return magazinePosition;
        }

        const float maximumHandDistance = 0.8f;
        if (!magazineView.gameObject.activeInHierarchy
            || (magazinePosition - leftPalm.position).sqrMagnitude > maximumHandDistance * maximumHandDistance)
        {
            return leftPalm.position;
        }

        return magazinePosition;
    }

    private void DrawDisconnectedScreen(float scale)
    {
        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.01f, 0.01f, 0.96f));
        UpdateDisconnectNoise();
        GUI.color = new Color(1f, 1f, 1f, 0.33f);
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _disconnectNoiseTexture,
            ScaleMode.StretchToFill,
            false);

        var textRect = new Rect(0f, Screen.height * 0.5f - 45f * scale, Screen.width, 90f * scale);
        DrawShadowedLabel(textRect, "Cam Disconnected", _disconnectStyle);
    }

    private static DateTime? GetCurrentRaidWorldTime()
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            return null;
        }

        var gameDateTime = Singleton<GameWorld>.Instance?.GameDateTime;
        return gameDateTime?.Calculate();
    }

    private void EnsureTextures()
    {
        if (_scanlineTexture == null)
        {
            _scanlineTexture = CreateScanlineTexture();
        }
        if (_recordingLabelTexture == null)
        {
            _recordingLabelTexture = LoadEmbeddedTexture("SenzeBodycam.Assets.recording-label.png");
        }
        if (_dimRecordingLabelTexture == null && _recordingLabelTexture != null)
        {
            _dimRecordingLabelTexture = CreateDimRecordingLabelTexture(_recordingLabelTexture);
        }
        if (_senzeLogoTexture == null)
        {
            _senzeLogoTexture = LoadEmbeddedTexture("SenzeBodycam.Assets.SenzeBodycamLogo.png");
        }
        if (_disconnectNoiseTexture == null)
        {
            _disconnectNoiseTexture = CreateDisconnectNoiseTexture();
        }
    }

    private void EnsureStyles(float scale)
    {
        EnsureBahnschriftFont();
        var fontSize = Mathf.Max(11, Mathf.RoundToInt(18f * scale));
        var magazineScale = scale * Mathf.Clamp(_settings.MagazineCheckLabelScale.Value, 0.6f, 1.8f);
        var magazineFontSize = Mathf.Max(11, Mathf.RoundToInt(15f * magazineScale));
        var recordingTextFontSize = Mathf.Max(8, Mathf.RoundToInt(22f * scale * _settings.RecordingTextScale.Value));
        if (_realBodycamPrimaryStyle != null
            && _cachedFontSize == fontSize
            && _cachedMagazineFontSize == magazineFontSize
            && _cachedRecordingTextFontSize == recordingTextFontSize)
        {
            return;
        }

        _cachedFontSize = fontSize;
        _cachedMagazineFontSize = magazineFontSize;
        _cachedRecordingTextFontSize = recordingTextFontSize;
        _realBodycamPrimaryStyle = CreateStyle(recordingTextFontSize, TextAnchor.MiddleRight, FontStyle.Normal);
        _realBodycamPrimaryStyle.font = _bahnschriftFont;
        _realBodycamPrimaryStyle.normal.textColor = new Color(0.96f, 0.96f, 0.96f, 0.96f);
        _realBodycamSecondaryStyle = CreateStyle(recordingTextFontSize, TextAnchor.MiddleRight, FontStyle.Normal);
        _realBodycamSecondaryStyle.font = _bahnschriftFont;
        _realBodycamSecondaryStyle.normal.textColor = new Color(0.96f, 0.96f, 0.96f, 0.96f);
        _magazineCountStyle = CreateStyle(magazineFontSize, TextAnchor.MiddleLeft, FontStyle.Bold);
        _magazineAmmoStyle = CreateStyle(Mathf.Max(10, Mathf.RoundToInt(13f * magazineScale)), TextAnchor.MiddleLeft, FontStyle.Normal);
        _disconnectStyle = CreateStyle(Mathf.Max(26, Mathf.RoundToInt(46f * scale)), TextAnchor.MiddleCenter, FontStyle.Bold);
        _disconnectStyle.normal.textColor = new Color(0.78f, 0.035f, 0.025f, 0.96f);
    }

    private void EnsureBahnschriftFont()
    {
        if (_bahnschriftFont != null)
        {
            return;
        }

        _bahnschriftFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Bahnschrift Light", "Bahnschrift" },
            48);
    }

    private static GUIStyle CreateStyle(int fontSize, TextAnchor alignment, FontStyle fontStyle)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = alignment,
            fontSize = fontSize,
            fontStyle = fontStyle,
            clipping = TextClipping.Clip
        };
        style.normal.textColor = TextColor;
        return style;
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        var previousGuiColor = GUI.color;
        GUI.color = Color.white;
        var originalColor = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = originalColor;
        GUI.Label(rect, text, style);
        GUI.color = previousGuiColor;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    private static Texture2D CreateScanlineTexture()
    {
        var texture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
        {
            name = "Senze Bodycam Scanlines",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(new[]
        {
            new Color(0f, 0f, 0f, 1f),
            Color.clear,
            Color.clear,
            Color.clear
        });
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateDisconnectNoiseTexture()
    {
        var texture = new Texture2D(160, 90, TextureFormat.RGBA32, false)
        {
            name = "Senze Bodycam Disconnect Noise",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };
        return texture;
    }

    private static Texture2D LoadEmbeddedTexture(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                return null;
            }

            var data = new byte[checked((int)stream.Length)];
            var offset = 0;
            while (offset < data.Length)
            {
                var read = stream.Read(data, offset, data.Length - offset);
                if (read <= 0)
                {
                    break;
                }
                offset += read;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = resourceName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (offset == data.Length && ImageConversion.LoadImage(texture, data, false))
            {
                return texture;
            }

            DestroyTexture(texture);
            return null;
        }
    }

    private static Texture2D CreateDimRecordingLabelTexture(Texture2D source)
    {
        var pixels = source.GetPixels32();
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            pixel.a = (byte)Mathf.RoundToInt(pixel.a * 0.18f);
            pixels[index] = pixel;
        }

        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            name = "Senze Bodycam Dim REC Label",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private void RefreshRaidContext()
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            ClearFaceCensorTextures();
            _raidOwner = null;
            _damageOwner = null;
            _disconnected = false;
            _projectileHits.Clear();
            ResetNativeLensDamage();
            return;
        }

        var player = _bodyCamera.OverlayPlayer;
        if (player != null && !ReferenceEquals(_raidOwner, player))
        {
            ClearFaceCensorTextures();
            _raidOwner = player;
            _damageOwner = player;
            _disconnected = false;
            _projectileHits.Clear();
            ResetNativeLensDamage();

        }

        if (_raidOwner?.HealthController != null && !_raidOwner.HealthController.IsAlive)
        {
            _disconnected = true;
        }
    }

    private void ResetNativeLensDamage()
    {
        _nativeLensHitCount = 0;
        Array.Clear(_nativeLensDamageMarks, 0, _nativeLensDamageMarks.Length);
        _nativeLensDamageDirty = true;
    }

    private void EnsureDamageOwner()
    {
        var player = _bodyCamera.OverlayPlayer;
        if (ReferenceEquals(_damageOwner, player))
        {
            return;
        }

        _damageOwner = player;
        _projectileHits.Clear();
        ResetNativeLensDamage();
    }

    private void UpdateDisconnectNoise()
    {
        if (_disconnectNoiseTexture == null || Time.unscaledTime < _nextNoiseUpdate)
        {
            return;
        }

        _nextNoiseUpdate = Time.unscaledTime + 0.08f;
        var pixels = new Color[_disconnectNoiseTexture.width * _disconnectNoiseTexture.height];
        for (var index = 0; index < pixels.Length; index++)
        {
            var value = 0.08f + (float)_hudRandom.NextDouble() * 0.52f;
            if (_hudRandom.NextDouble() < 0.035d)
            {
                value = 0.85f;
            }
            pixels[index] = new Color(value, value, value, 1f);
        }

        _disconnectNoiseTexture.SetPixels(pixels);
        _disconnectNoiseTexture.Apply(false, false);
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    private sealed class FaceMosaic
    {
        internal Texture2D Capture { get; set; }
        internal Texture2D Texture { get; set; }
        internal Rect ScreenRect { get; set; }
        internal Vector3 WorldCentre { get; set; }
        internal float WorldWidth { get; set; }
        internal float WorldHeight { get; set; }
        internal Vector3 WorldUp { get; set; }
        internal Vector3 HeadLocalFaceDirection { get; set; }
        internal bool HasHeadLocalFaceDirection { get; set; }
        internal Vector3 HeadLocalUpDirection { get; set; }
        internal bool HasHeadLocalUpDirection { get; set; }
        internal int LastVisibleFrame { get; set; }
        internal float NextCaptureTime { get; set; }
    }

    private readonly struct LensDamageMark
    {
        internal LensDamageMark(Vector2 position, float size, float rotation)
        {
            Position = position;
            Size = size;
            Rotation = rotation;
        }

        internal Vector2 Position { get; }
        internal float Size { get; }
        internal float Rotation { get; }
    }

    private struct ProjectileHitKey : IEquatable<ProjectileHitKey>
    {
        private readonly string _ammoId;
        private readonly int _fireIndex;
        private readonly int _fragmentIndex;

        internal ProjectileHitKey(string ammoId, int fireIndex, int fragmentIndex)
        {
            _ammoId = ammoId;
            _fireIndex = fireIndex;
            _fragmentIndex = fragmentIndex;
        }

        public bool Equals(ProjectileHitKey other)
        {
            return _fireIndex == other._fireIndex
                && _fragmentIndex == other._fragmentIndex
                && string.Equals(_ammoId, other._ammoId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectileHitKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _ammoId != null ? _ammoId.GetHashCode() : 0;
                hash = hash * 397 ^ _fireIndex;
                return hash * 397 ^ _fragmentIndex;
            }
        }
    }
}
