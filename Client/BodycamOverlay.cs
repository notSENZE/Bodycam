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

    private static readonly Color HudColor = new Color(0.64f, 0.65f, 0.62f, 0.82f);
    private static readonly Color TextColor = new Color(0.78f, 0.79f, 0.76f, 0.91f);
    private static readonly Color ShadowColor = new Color(0.01f, 0.012f, 0.01f, 0.62f);
    private static readonly Color RecordingColor = new Color(0.74f, 0.08f, 0.06f, 0.94f);
    private static readonly Vector2[] LightningPoints =
    {
        new Vector2(0.10f, 0.94f),
        new Vector2(-0.58f, 0.02f),
        new Vector2(-0.18f, 0.02f),
        new Vector2(-0.42f, -0.94f),
        new Vector2(0.62f, 0.18f),
        new Vector2(0.18f, 0.18f),
        new Vector2(0.48f, 0.94f)
    };

    private readonly ModConfig _settings;
    private readonly BodyCameraController _bodyCamera;
    private readonly HashSet<ProjectileHitKey> _projectileHits = new HashSet<ProjectileHitKey>();
    private readonly Dictionary<string, FaceMosaic> _faceMosaics = new Dictionary<string, FaceMosaic>();
    private readonly RaycastHit[] _faceOcclusionHits = new RaycastHit[24];
    private readonly System.Random _hudRandom = new System.Random();

    private Texture2D _scanlineTexture;
    private Texture2D _recordingDotTexture;
    private Texture2D[] _statusFillTextures;
    private Texture2D[] _statusOutlineTextures;
    private Texture2D _disconnectNoiseTexture;
    private Texture2D[] _wifiTextures;
    private Texture2D[] _batteryTextures;
    private Texture2D _batteryWarningTexture;
    private Camera _nativeRainCamera;
    private RainScreenDrops _nativeRainScreenDrops;
    private bool _nativeRainGlassForced;
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
    private float _nativeVisorMinLightValue;
    private bool _nativeVisorAffectedByLight;
    private RenderTexture _nativeLensDamageTexture;
    private Texture2D _nativeLensDamageDisplayTexture;
    private int _nativeLensHitCount;
    private byte _nativeLensHitSeed;
    private bool _nativeLensDamageDirty;
    private GUIStyle _operatorStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _statusCountStyle;
    private GUIStyle _leftStyle;
    private GUIStyle _magazineCountStyle;
    private GUIStyle _magazineAmmoStyle;
    private GUIStyle _disconnectStyle;
    private int _cachedFontSize;
    private int _cachedMagazineFontSize;
    private int _damageSeed;
    private Player _damageOwner;
    private Player _raidOwner;
    private string _raidLocationName = "UNKNOWN";
    private bool _disconnected;
    private int _wifiBars = 3;
    private float _nextWifiChange;
    private float _nextNoiseUpdate;
    private float _nextFlickerJitterUpdate;
    private float _flickerJitter = 0.5f;
    private float _nextGrenadeCountUpdate;
    private int _readyGrenadeCount;

    internal BodycamOverlay(ModConfig settings, BodyCameraController bodyCamera)
    {
        _settings = settings;
        _bodyCamera = bodyCamera;
    }

    internal void Draw()
    {
        RefreshRaidContext();
        UpdateNativeRainLensEffect();
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
        GUI.depth = -1000;

        if (_disconnected)
        {
            DrawDisconnectedScreen(scale);
        }
        else
        {
            DrawLensEffects();
            DrawNativeLensDamage();
            DrawCornerMarkers(scale);
            DrawSideMarkers(scale);
            DrawRecordingHeader(scale);
            DrawWirelessStatus(scale);
            DrawBattery(scale);
            DrawRaidInformation(scale);
            DrawStatusHud(scale);
            DrawMagazineCheckLabel(scale);
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    internal void Dispose()
    {
        RestoreNativeRainLensEffect();
        RestoreNativeVisorLensEffect();
        RemoveFaceMosaicCapture();
        DestroyFaceMosaicRenderer();
        DestroyTexture(_scanlineTexture);
        DestroyTexture(_recordingDotTexture);
        DestroyTexture(_disconnectNoiseTexture);
        if (_wifiTextures != null)
        {
            foreach (var texture in _wifiTextures)
            {
                DestroyTexture(texture);
            }
        }
        if (_batteryTextures != null)
        {
            foreach (var texture in _batteryTextures)
            {
                DestroyTexture(texture);
            }
        }
        DestroyTexture(_batteryWarningTexture);
        if (_nativeLensDamageTexture != null)
        {
            UnityEngine.Object.Destroy(_nativeLensDamageTexture);
        }
        DestroyTexture(_nativeLensDamageDisplayTexture);
        if (_statusFillTextures != null)
        {
            foreach (var texture in _statusFillTextures)
            {
                DestroyTexture(texture);
            }
        }
        if (_statusOutlineTextures != null)
        {
            foreach (var texture in _statusOutlineTextures)
            {
                DestroyTexture(texture);
            }
        }
        _scanlineTexture = null;
        _recordingDotTexture = null;
        _disconnectNoiseTexture = null;
        _wifiTextures = null;
        _batteryTextures = null;
        _batteryWarningTexture = null;
        _nativeLensDamageTexture = null;
        _nativeLensDamageDisplayTexture = null;
        _statusFillTextures = null;
        _statusOutlineTextures = null;
        ClearFaceCensorTextures();
        _damageOwner = null;
        _raidOwner = null;
        _projectileHits.Clear();
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

        _damageSeed++;
        _nativeLensHitCount++;
        if (_nativeLensHitSeed == 0)
        {
            _nativeLensHitSeed = (byte)Mathf.Clamp((_damageSeed * 37 + 17) & 0xff, 1, 255);
        }
        _nativeLensDamageDirty = true;
    }

    private void DrawLensEffects()
    {
        var previousColor = GUI.color;
        var noiseStrength = Mathf.Clamp01(_settings.SensorNoiseStrength.Value);
        var flickerStrength = Mathf.Clamp01(_settings.SignalFlickerStrength.Value);
        if (flickerStrength > 0f)
        {
            var flicker = GetSignalFlicker();
            DrawRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0f, 0f, 0f, flickerStrength * Mathf.Lerp(0.012f, 0.042f, flicker)));
        }

        GUI.color = new Color(1f, 1f, 1f, noiseStrength * 0.02f);
        var scanlineOffset = Mathf.Repeat(Time.unscaledTime * 7f, 1f);
        GUI.DrawTextureWithTexCoords(
            new Rect(0f, 0f, Screen.width, Screen.height),
            _scanlineTexture,
            new Rect(0f, scanlineOffset, 1f, Screen.height / 4f));
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

        foreach (var corpse in gameWorld.LootList.OfType<Corpse>())
        {
            if (corpse == null || !corpse.gameObject.activeInHierarchy)
            {
                continue;
            }

            var playerBody = corpse.GetComponentInChildren<PlayerBody>();
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
        }

        var faceDirection = mosaic.HasHeadLocalFaceDirection
            ? head.TransformDirection(mosaic.HeadLocalFaceDirection).normalized
            : referenceDirection.normalized;

        var faceCentre = head.position + faceDirection.normalized * FaceForwardOffset;
        var cameraDistance = Vector3.Distance(faceCentre, camera.transform.position);
        var maximumDistance = _settings.CensorshipMaximumDistance.Value;
        if (cameraDistance > maximumDistance
            || !TryGetFaceRect(camera, faceCentre, sizeScale, out var faceRect)
            || !IsFaceVisible(gameWorld, localPlayer, targetPlayer, targetRoot, faceCentre))
        {
            return;
        }

        mosaic.ScreenRect = faceRect;
        mosaic.WorldCentre = faceCentre;
        mosaic.WorldSize = 0.27f * sizeScale;
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

            var faceDepthClearance = Mathf.Max(0.06f, mosaic.WorldSize * 0.28f);
            var renderPosition = mosaic.WorldCentre + directionToCamera.normalized * faceDepthClearance;
            var renderRotation = Quaternion.LookRotation(directionToCamera, camera.transform.up);
            var renderScale = new Vector3(mosaic.WorldSize, mosaic.WorldSize, 1f);
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

    private static bool TryGetFaceRect(Camera camera, Vector3 faceCentre, float sizeScale, out Rect rect)
    {
        rect = default;
        var screenCentre = camera.WorldToScreenPoint(faceCentre);
        if (screenCentre.z <= 0.05f
            || screenCentre.x < -100f
            || screenCentre.x > Screen.width + 100f
            || screenCentre.y < -100f
            || screenCentre.y > Screen.height + 100f)
        {
            return false;
        }

        var cameraTransform = camera.transform;
        var screenRight = camera.WorldToScreenPoint(faceCentre + cameraTransform.right * 0.105f);
        var screenTop = camera.WorldToScreenPoint(faceCentre + cameraTransform.up * 0.135f);
        var width = Mathf.Abs(screenRight.x - screenCentre.x) * 2f * sizeScale;
        var height = Mathf.Abs(screenTop.y - screenCentre.y) * 2f * sizeScale;
        var minimumFaceSize = Mathf.Max(
            MinimumCensorshipScreenSize,
            Screen.height * MinimumCensorshipScreenHeightRatio);
        if (width < minimumFaceSize || height < minimumFaceSize)
        {
            return false;
        }

        var side = Mathf.Min(
            Mathf.Max(width, height),
            Mathf.Min(Screen.width * 0.34f, Screen.height * 0.46f));
        rect = new Rect(
            screenCentre.x - side * 0.5f,
            Screen.height - screenCentre.y - side * 0.5f,
            side,
            side);
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
        var mosaicSize = Mathf.RoundToInt(Mathf.Lerp(configuredBlockCount, distantBlockCount, distanceRatio));
        if (mosaic.Texture == null
            || mosaic.Texture.width != mosaicSize
            || mosaic.Texture.height != mosaicSize)
        {
            DestroyTexture(mosaic.Texture);
            mosaic.Texture = new Texture2D(mosaicSize, mosaicSize, TextureFormat.RGB24, false)
            {
                name = "Senze Bodycam Privacy Mosaic",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var sourcePixels = mosaic.Capture.GetPixels32();
        var mosaicPixels = new Color32[mosaicSize * mosaicSize];
        for (var mosaicY = 0; mosaicY < mosaicSize; mosaicY++)
        {
            var sourceYStart = Mathf.FloorToInt(mosaicY * height / (float)mosaicSize);
            var sourceYEnd = Mathf.Max(sourceYStart + 1, Mathf.CeilToInt((mosaicY + 1) * height / (float)mosaicSize));
            for (var mosaicX = 0; mosaicX < mosaicSize; mosaicX++)
            {
                var sourceXStart = Mathf.FloorToInt(mosaicX * width / (float)mosaicSize);
                var sourceXEnd = Mathf.Max(sourceXStart + 1, Mathf.CeilToInt((mosaicX + 1) * width / (float)mosaicSize));
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

                mosaicPixels[mosaicY * mosaicSize + mosaicX] = new Color32(
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
        UnityEngine.Random.InitState(_nativeLensHitSeed);
        for (var index = 0; index < _nativeLensHitCount; index++)
        {
            var position = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f));
            var size = UnityEngine.Random.Range(1.6f, 2.6f);
            var scale = new Vector3(size / _nativeVisorCamera.aspect, size);
            var rotation = UnityEngine.Random.Range(0f, 360f);
            _nativeVisorEffect.Draw(position, scale, rotation);
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

    private void UpdateNativeRainLensEffect()
    {
        var camera = _bodyCamera.OverlayCamera;
        if (!ReferenceEquals(_nativeRainCamera, camera))
        {
            RestoreNativeRainLensEffect();
            _nativeRainCamera = camera;
            _nativeRainScreenDrops = camera != null
                ? camera.GetComponent<RainScreenDrops>()
                    ?? camera.GetComponent<EffectsController>()?.RainScreenDrops
                : null;
        }

        var shouldUseBodycamLens = _nativeRainScreenDrops != null
            && _settings.ModEnabled.Value
            && _settings.BodyCameraEnabled.Value
            && _settings.RainLensEffectsEnabled.Value
            && _bodyCamera.ShouldDrawOverlay;
        if (!shouldUseBodycamLens)
        {
            RestoreNativeRainLensEffect();
            return;
        }

        if (!_nativeRainGlassForced)
        {
            _nativeRainScreenDrops.ChangeGlassesState(true);
            _nativeRainGlassForced = true;
        }
    }

    private void RestoreNativeRainLensEffect()
    {
        if (_nativeRainScreenDrops == null || !_nativeRainGlassForced)
        {
            return;
        }

        _nativeRainScreenDrops.ChangeGlassesState(HasActivePlayerVisor());
        _nativeRainGlassForced = false;
    }

    private bool HasActivePlayerVisor()
    {
        var player = _bodyCamera.OverlayPlayer;
        if (player == null || !player.TryFindGlasses(out var visor) || visor == null)
        {
            return false;
        }

        return visor.Togglable == null || visor.Togglable.On;
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

    private void DrawCornerMarkers(float scale)
    {
        var margin = 42f * scale;
        var length = 112f * scale;
        var thickness = Mathf.Max(1f, 2f * scale);

        DrawCorner(margin, margin, length, thickness, horizontalRight: true, verticalDown: true);
        DrawCorner(Screen.width - margin, margin, length, thickness, horizontalRight: false, verticalDown: true);
        DrawCorner(margin, Screen.height - margin, length, thickness, horizontalRight: true, verticalDown: false);
        DrawCorner(Screen.width - margin, Screen.height - margin, length, thickness, horizontalRight: false, verticalDown: false);
    }

    private void DrawSideMarkers(float scale)
    {
        var margin = 42f * scale;
        var length = 112f * scale;
        var thickness = Mathf.Max(1f, 2f * scale);
        var top = (Screen.height - length) * 0.5f;

        DrawRect(new Rect(margin + 2f, top + 2f, thickness, length), ShadowColor);
        DrawRect(new Rect(Screen.width - margin + 2f, top + 2f, thickness, length), ShadowColor);
        DrawRect(new Rect(margin, top, thickness, length), HudColor);
        DrawRect(new Rect(Screen.width - margin, top, thickness, length), HudColor);
    }

    private void DrawCorner(
        float x,
        float y,
        float length,
        float thickness,
        bool horizontalRight,
        bool verticalDown)
    {
        var horizontalX = horizontalRight ? x : x - length;
        var verticalY = verticalDown ? y : y - length;

        DrawRect(new Rect(horizontalX + 2f, y + 2f, length, thickness), ShadowColor);
        DrawRect(new Rect(x + 2f, verticalY + 2f, thickness, length), ShadowColor);
        DrawRect(new Rect(horizontalX, y, length, thickness), HudColor);
        DrawRect(new Rect(x, verticalY, thickness, length), HudColor);
    }

    private void DrawRecordingHeader(float scale)
    {
        var centreX = Screen.width * 0.5f + _settings.RecordingOffsetX.Value * scale;
        var top = (42f + _settings.RecordingOffsetY.Value) * scale;
        var lineWidth = 130f * scale;
        var lineHeight = 28f * scale;
        var lineRect = new Rect(centreX - lineWidth * 0.5f, top, lineWidth, lineHeight);
        DrawShadowedLabel(lineRect, "REC", _operatorStyle);

        if (Mathf.FloorToInt(Time.unscaledTime * 1.6f) % 2 == 0)
        {
            var dotSize = 15f * scale;
            var dotRect = new Rect(lineRect.x + 17f * scale, top + (lineHeight - dotSize) * 0.5f, dotSize, dotSize);
            GUI.color = RecordingColor;
            GUI.DrawTexture(dotRect, _recordingDotTexture);
        }

        var operatorName = _raidOwner?.Profile?.Info?.Nickname;
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            operatorName = "OPERATOR";
        }

        var operatorRect = new Rect(centreX - 210f * scale, top + 25f * scale, 420f * scale, 26f * scale);
        DrawShadowedLabel(operatorRect, $"Operator: {operatorName}", _smallStyle);
    }

    private void DrawWirelessStatus(float scale)
    {
        if (_wifiTextures == null || _wifiTextures.Length < 3)
        {
            return;
        }

        if (Time.unscaledTime >= _nextWifiChange)
        {
            _wifiBars = _hudRandom.Next(1, 4);
            _nextWifiChange = Time.unscaledTime + 0.65f + (float)_hudRandom.NextDouble() * 1.35f;
        }

        var rect = new Rect(
            Screen.width - 224f * scale + _settings.WirelessOffsetX.Value * scale,
            (66f + _settings.WirelessOffsetY.Value) * scale,
            64f * scale,
            40f * scale);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, _wifiTextures[_wifiBars - 1], ScaleMode.ScaleToFit, true);
    }

    private void DrawBattery(float scale)
    {
        if (_batteryTextures == null || _batteryTextures.Length < 3)
        {
            return;
        }

        var hasRaidTime = TryGetRaidTimes(out var remainingTime, out var sessionTime);
        var remainingRatio = hasRaidTime
            ? Mathf.Clamp01((float)(remainingTime.TotalSeconds / sessionTime.TotalSeconds))
            : 1f;
        var criticalBattery = hasRaidTime
            && remainingTime.TotalSeconds > 0d
            && remainingTime.TotalMinutes <= 10d;
        var textureIndex = criticalBattery ? 0 : remainingRatio > 0.66f ? 2 : remainingRatio > 0.33f ? 1 : 0;
        var rect = new Rect(
            Screen.width - 132f * scale + _settings.BatteryOffsetX.Value * scale,
            (66f + _settings.BatteryOffsetY.Value) * scale,
            78f * scale,
            40f * scale);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, _batteryTextures[textureIndex], ScaleMode.ScaleToFit, true);

        if (criticalBattery && _batteryWarningTexture != null)
        {
            var blink = 0.18f + (Mathf.Sin(Time.unscaledTime * Mathf.PI * 3.2f) + 1f) * 0.41f;
            GUI.color = new Color(0.95f, 0.035f, 0.02f, blink);
            GUI.DrawTexture(rect, _batteryWarningTexture, ScaleMode.ScaleToFit, true);
        }
    }

    private void DrawRaidInformation(float scale)
    {
        var raidTime = GetCurrentRaidWorldTime();
        var displayedTime = raidTime.HasValue
            ? raidTime.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            : "--:--:--";
        var date = DateTime.Now.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        var rect = new Rect(
            (52f + _settings.RaidInfoOffsetX.Value) * scale,
            Screen.height - 132f * scale + _settings.RaidInfoOffsetY.Value * scale,
            420f * scale,
            82f * scale);
        DrawShadowedLabel(rect, $"{date}\nRAID TIME: {displayedTime}\nLOCATION: {_raidLocationName}", _leftStyle);
    }

    private void DrawStatusHud(float scale)
    {
        if (_raidOwner?.HealthController == null
            || _statusFillTextures == null
            || _statusOutlineTextures == null)
        {
            return;
        }

        var healthController = _raidOwner.HealthController;
        var health = Mathf.Clamp01(healthController.GetBodyPartHealth(EBodyPart.Common).Normalized);
        var hydration = Mathf.Clamp01(healthController.Hydration.Normalized);
        var energy = Mathf.Clamp01(healthController.Energy.Normalized);
        var grenadeCount = GetReadyGrenadeCount();

        var width = 244f * scale;
        var left = Screen.width - 52f * scale - width + _settings.StatusHudOffsetX.Value * scale;
        var top = Screen.height - 124f * scale + _settings.StatusHudOffsetY.Value * scale;

        var iconSize = 40f * scale;
        var iconTop = top + 11f * scale;
        var healthRect = new Rect(left + 12f * scale, iconTop, iconSize, iconSize);
        if (HasHeavyBleeding())
        {
            DrawHeavyBleedingHalo(healthRect, scale);
        }

        DrawStatusIcon(healthRect, StatusIconKind.Health, health);
        DrawStatusIcon(new Rect(left + 62f * scale, iconTop, iconSize, iconSize), StatusIconKind.Hydration, hydration);
        DrawStatusIcon(new Rect(left + 112f * scale, iconTop, iconSize, iconSize), StatusIconKind.Energy, energy);

        var grenadeRect = new Rect(left + 168f * scale, iconTop, iconSize, iconSize);
        DrawStatusIcon(grenadeRect, StatusIconKind.Grenade, grenadeCount > 0 ? 1f : 0f);
        DrawShadowedLabel(
            new Rect(left + 207f * scale, top + 10f * scale, 31f * scale, 42f * scale),
            grenadeCount.ToString(CultureInfo.InvariantCulture),
            _statusCountStyle);
    }

    private void DrawStatusIcon(Rect rect, StatusIconKind kind, float fillRatio)
    {
        var textureIndex = (int)kind;
        fillRatio = Mathf.Clamp01(fillRatio);
        if (fillRatio > 0.001f)
        {
            var fillHeight = rect.height * fillRatio;
            var fillRect = new Rect(rect.x, rect.yMax - fillHeight, rect.width, fillHeight);
            var fillColor = GetStatusFillColor(fillRatio);
            GUI.color = fillColor;
            GUI.DrawTextureWithTexCoords(
                fillRect,
                _statusFillTextures[textureIndex],
                new Rect(0f, 0f, 1f, fillRatio),
                true);
        }

        GUI.color = new Color(HudColor.r, HudColor.g, HudColor.b, 0.92f);
        GUI.DrawTexture(rect, _statusOutlineTextures[textureIndex], ScaleMode.ScaleToFit, true);
    }

    private static Color GetStatusFillColor(float fillRatio)
    {
        if (fillRatio <= 0.15f)
        {
            return new Color(0.68f, 0.10f, 0.07f, 0.92f);
        }
        if (fillRatio <= 0.35f)
        {
            return new Color(0.65f, 0.44f, 0.16f, 0.90f);
        }
        return new Color(HudColor.r, HudColor.g, HudColor.b, 0.84f);
    }

    private bool HasHeavyBleeding()
    {
        if (_raidOwner?.HealthController == null)
        {
            return false;
        }

        foreach (var effect in _raidOwner.HealthController.FindActiveEffects<IHeavyBleeding>(EBodyPart.Common))
        {
            return true;
        }

        return false;
    }

    private void DrawHeavyBleedingHalo(Rect heartRect, float scale)
    {
        var strength = _settings.HeavyBleedingHaloStrength.Value;
        if (strength <= 0.001f)
        {
            return;
        }

        var pulse = 0.78f + (Mathf.Sin(Time.unscaledTime * 5.5f) + 1f) * 0.11f;
        var alpha = Mathf.Clamp01(strength * pulse);
        var heartTexture = _statusFillTextures[(int)StatusIconKind.Health];
        DrawHeavyBleedingHaloLayer(heartRect, heartTexture, 11f * scale, alpha * 0.10f);
        DrawHeavyBleedingHaloLayer(heartRect, heartTexture, 7f * scale, alpha * 0.16f);
        DrawHeavyBleedingHaloLayer(heartRect, heartTexture, 4f * scale, alpha * 0.24f);
    }

    private static void DrawHeavyBleedingHaloLayer(Rect heartRect, Texture2D heartTexture, float expansion, float alpha)
    {
        var haloRect = new Rect(
            heartRect.x - expansion,
            heartRect.y - expansion,
            heartRect.width + expansion * 2f,
            heartRect.height + expansion * 2f);
        GUI.color = new Color(0.92f, 0.035f, 0.018f, alpha);
        GUI.DrawTexture(haloRect, heartTexture, ScaleMode.ScaleToFit, true);
    }

    private int GetReadyGrenadeCount()
    {
        if (_raidOwner?.InventoryController == null)
        {
            return 0;
        }

        if (Time.unscaledTime >= _nextGrenadeCountUpdate)
        {
            var grenades = InventoryExtension.GetThrowablePriorityGrenadesList(_raidOwner.InventoryController);
            _readyGrenadeCount = grenades?.Count ?? 0;
            _nextGrenadeCountUpdate = Time.unscaledTime + 0.25f;
        }

        return _readyGrenadeCount;
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

    private static bool TryGetRaidTimes(out TimeSpan remaining, out TimeSpan sessionTime)
    {
        remaining = TimeSpan.Zero;
        sessionTime = TimeSpan.Zero;
        if (!Singleton<AbstractGame>.Instantiated)
        {
            return false;
        }

        var timer = Singleton<AbstractGame>.Instance?.GameTimer;
        if (timer?.SessionTime == null || timer.SessionTime.Value.TotalSeconds <= 0d)
        {
            return false;
        }

        sessionTime = timer.SessionTime.Value;
        remaining = sessionTime - timer.PastTime;
        return true;
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
        if (_recordingDotTexture == null)
        {
            _recordingDotTexture = CreateDotTexture();
        }
        if (_statusFillTextures == null || _statusOutlineTextures == null)
        {
            var iconCount = Enum.GetValues(typeof(StatusIconKind)).Length;
            _statusFillTextures = new Texture2D[iconCount];
            _statusOutlineTextures = new Texture2D[iconCount];
            for (var index = 0; index < iconCount; index++)
            {
                var kind = (StatusIconKind)index;
                _statusFillTextures[index] = CreateStatusIconTexture(kind, false);
                _statusOutlineTextures[index] = CreateStatusIconTexture(kind, true);
            }
        }
        if (_disconnectNoiseTexture == null)
        {
            _disconnectNoiseTexture = CreateDisconnectNoiseTexture();
        }
        if (_wifiTextures == null)
        {
            _wifiTextures = LoadHudTextures(
                "SenzeBodycam.Assets.wifi-low.png",
                "SenzeBodycam.Assets.wifi-medium.png",
                "SenzeBodycam.Assets.wifi-high.png");
        }
        if (_batteryTextures == null)
        {
            _batteryTextures = LoadHudTextures(
                "SenzeBodycam.Assets.battery-low.png",
                "SenzeBodycam.Assets.battery-medium.png",
                "SenzeBodycam.Assets.battery-high.png");
        }
        if (_batteryWarningTexture == null && _batteryTextures != null && _batteryTextures.Length > 0)
        {
            _batteryWarningTexture = CreateBatteryWarningTexture(_batteryTextures[0]);
        }
    }

    private void EnsureStyles(float scale)
    {
        var fontSize = Mathf.Max(11, Mathf.RoundToInt(18f * scale));
        var magazineScale = scale * Mathf.Clamp(_settings.MagazineCheckLabelScale.Value, 0.6f, 1.8f);
        var magazineFontSize = Mathf.Max(11, Mathf.RoundToInt(15f * magazineScale));
        if (_operatorStyle != null
            && _cachedFontSize == fontSize
            && _cachedMagazineFontSize == magazineFontSize)
        {
            return;
        }

        _cachedFontSize = fontSize;
        _cachedMagazineFontSize = magazineFontSize;
        _operatorStyle = CreateStyle(fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
        _smallStyle = CreateStyle(Mathf.Max(10, Mathf.RoundToInt(13f * scale)), TextAnchor.MiddleCenter, FontStyle.Bold);
        _statusCountStyle = CreateStyle(Mathf.Max(12, Mathf.RoundToInt(18f * scale)), TextAnchor.MiddleLeft, FontStyle.Bold);
        _leftStyle = CreateStyle(Mathf.Max(10, Mathf.RoundToInt(15f * scale)), TextAnchor.LowerLeft, FontStyle.Bold);
        _leftStyle.wordWrap = false;
        _magazineCountStyle = CreateStyle(magazineFontSize, TextAnchor.MiddleLeft, FontStyle.Bold);
        _magazineAmmoStyle = CreateStyle(Mathf.Max(10, Mathf.RoundToInt(13f * magazineScale)), TextAnchor.MiddleLeft, FontStyle.Normal);
        _disconnectStyle = CreateStyle(Mathf.Max(26, Mathf.RoundToInt(46f * scale)), TextAnchor.MiddleCenter, FontStyle.Bold);
        _disconnectStyle.normal.textColor = new Color(0.78f, 0.035f, 0.025f, 0.96f);
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

    private static Texture2D CreateDotTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Senze Bodycam Recording Dot",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateBatteryWarningTexture(Texture2D lowBatteryTexture)
    {
        var width = lowBatteryTexture.width;
        var height = lowBatteryTexture.height;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Senze Bodycam Battery Warning",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var sourcePixels = lowBatteryTexture.GetPixels32();
        var pixels = new Color32[sourcePixels.Length];
        var minimumX = Mathf.RoundToInt(width * (30f / 256f));
        var maximumX = Mathf.RoundToInt(width * (77f / 256f));
        var minimumY = Mathf.RoundToInt(height * (49f / 144f));
        var maximumY = Mathf.RoundToInt(height * (110f / 144f));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (x >= minimumX && x <= maximumX
                    && y >= minimumY && y <= maximumY
                    && sourcePixels[index].a > 0)
                {
                    pixels[index] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, sourcePixels[index].a);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateStatusIconTexture(StatusIconKind kind, bool outline)
    {
        const int size = 64;
        const int samplesPerAxis = 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"Senze Bodycam {kind} {(outline ? "Outline" : "Fill")}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var coveredSamples = 0;
                for (var sampleY = 0; sampleY < samplesPerAxis; sampleY++)
                {
                    for (var sampleX = 0; sampleX < samplesPerAxis; sampleX++)
                    {
                        var normalizedX = (x + (sampleX + 0.5f) / samplesPerAxis) / size * 2f - 1f;
                        var normalizedY = (y + (sampleY + 0.5f) / samplesPerAxis) / size * 2f - 1f;
                        var inside = IsInsideStatusIcon(kind, normalizedX, normalizedY);
                        if (inside && (!outline || IsStatusIconEdge(kind, normalizedX, normalizedY)))
                        {
                            coveredSamples++;
                        }
                    }
                }

                var alpha = coveredSamples / (float)(samplesPerAxis * samplesPerAxis);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static bool IsStatusIconEdge(StatusIconKind kind, float x, float y)
    {
        const float edgeWidth = 0.075f;
        return !IsInsideStatusIcon(kind, x - edgeWidth, y)
            || !IsInsideStatusIcon(kind, x + edgeWidth, y)
            || !IsInsideStatusIcon(kind, x, y - edgeWidth)
            || !IsInsideStatusIcon(kind, x, y + edgeWidth);
    }

    private static bool IsInsideStatusIcon(StatusIconKind kind, float x, float y)
    {
        switch (kind)
        {
            case StatusIconKind.Health:
                x *= 1.22f;
                y *= 1.22f;
                var heartShape = x * x + y * y - 1f;
                return heartShape * heartShape * heartShape - x * x * y * y * y <= 0f;

            case StatusIconKind.Hydration:
                var dropX = x;
                var dropY = y + 0.27f;
                var roundDrop = dropX * dropX + dropY * dropY <= 0.58f * 0.58f;
                var taperedDrop = y >= -0.10f
                    && y <= 0.92f
                    && Mathf.Abs(x) <= (0.92f - y) * 0.57f;
                return roundDrop || taperedDrop;

            case StatusIconKind.Energy:
                return IsInsidePolygon(LightningPoints, x, y);

            case StatusIconKind.Grenade:
                var bodyX = x / 0.58f;
                var bodyY = (y + 0.27f) / 0.60f;
                var grenadeBody = bodyX * bodyX + bodyY * bodyY <= 1f;
                var grenadeNeck = Mathf.Abs(x) <= 0.23f && y >= 0.22f && y <= 0.48f;
                var grenadeCap = Mathf.Abs(x) <= 0.34f && y >= 0.43f && y <= 0.58f;
                var grenadeLever = x >= 0.18f && x <= 0.68f && y >= 0.48f && y <= 0.62f;
                return grenadeBody || grenadeNeck || grenadeCap || grenadeLever;

            default:
                return false;
        }
    }

    private static bool IsInsidePolygon(Vector2[] points, float x, float y)
    {
        var inside = false;
        var previous = points.Length - 1;
        for (var current = 0; current < points.Length; current++)
        {
            var currentPoint = points[current];
            var previousPoint = points[previous];
            if ((currentPoint.y > y) != (previousPoint.y > y)
                && x < (previousPoint.x - currentPoint.x) * (y - currentPoint.y)
                    / (previousPoint.y - currentPoint.y) + currentPoint.x)
            {
                inside = !inside;
            }
            previous = current;
        }
        return inside;
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

    private static Texture2D[] LoadHudTextures(params string[] resourceNames)
    {
        var textures = new List<Texture2D>(resourceNames.Length);
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in resourceNames)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    continue;
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
                    RecolorHudTexture(texture, resourceName);
                    textures.Add(texture);
                }
                else
                {
                    DestroyTexture(texture);
                }
            }
        }

        return textures.ToArray();
    }

    private static void RecolorHudTexture(Texture2D texture, string resourceName)
    {
        var pixels = texture.GetPixels();
        for (var index = 0; index < pixels.Length; index++)
        {
            var source = pixels[index];
            if (source.a <= 0f)
            {
                continue;
            }

            pixels[index] = new Color(HudColor.r, HudColor.g, HudColor.b, source.a * 0.92f);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
    }

    private void RefreshRaidContext()
    {
        if (!Singleton<GameWorld>.Instantiated)
        {
            ClearFaceCensorTextures();
            _raidOwner = null;
            _damageOwner = null;
            _raidLocationName = "UNKNOWN";
            _disconnected = false;
            ResetStatusHud();
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
            ResetStatusHud();
            _projectileHits.Clear();
            ResetNativeLensDamage();

            var gameWorld = Singleton<GameWorld>.Instance;
            _raidLocationName = GetLocationName(gameWorld?.LocationId);
        }

        if (_raidOwner?.HealthController != null && !_raidOwner.HealthController.IsAlive)
        {
            _disconnected = true;
        }
    }

    private void ResetStatusHud()
    {
        _readyGrenadeCount = 0;
        _nextGrenadeCountUpdate = 0f;
    }

    private void ResetNativeLensDamage()
    {
        _nativeLensHitCount = 0;
        _nativeLensHitSeed = 0;
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

    private float GetSignalFlicker()
    {
        var now = Time.unscaledTime;
        if (now >= _nextFlickerJitterUpdate)
        {
            _flickerJitter = (float)_hudRandom.NextDouble();
            _nextFlickerJitterUpdate = now + 0.04f + (float)_hudRandom.NextDouble() * 0.08f;
        }

        var slowWave = Mathf.Sin(now * 13.7f) * 0.24f;
        var fastWave = Mathf.Sin(now * 37.1f) * 0.11f;
        return Mathf.Clamp01(0.48f + slowWave + fastWave + (_flickerJitter - 0.5f) * 0.24f);
    }

    private static string GetLocationName(string locationId)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return "UNKNOWN";
        }

        var localizationKey = locationId + " Name";
        var localized = localizationKey.Localized();
        if (!string.IsNullOrWhiteSpace(localized)
            && !string.Equals(localized, localizationKey, StringComparison.OrdinalIgnoreCase))
        {
            return localized.ToUpperInvariant();
        }

        var normalized = locationId.Replace('_', ' ').Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant()).ToUpperInvariant();
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
        internal float WorldSize { get; set; }
        internal Vector3 HeadLocalFaceDirection { get; set; }
        internal bool HasHeadLocalFaceDirection { get; set; }
        internal int LastVisibleFrame { get; set; }
        internal float NextCaptureTime { get; set; }
    }

    private enum StatusIconKind
    {
        Health,
        Hydration,
        Energy,
        Grenade
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
