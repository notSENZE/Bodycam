using UnityEngine;

namespace SenzeBodycam;

internal sealed class FaceMosaicCapture : MonoBehaviour
{
    private Camera _camera;

    private void OnPreRender()
    {
        EnsureCamera();
        PatchContext.BodycamOverlay?.PrepareNativeVisorForRender(_camera);
    }

    private void OnPostRender()
    {
        EnsureCamera();
        var overlay = PatchContext.BodycamOverlay;
        overlay?.CaptureFaceMosaics(_camera);
        overlay?.RenderFaceMosaics(_camera);
    }

    private void EnsureCamera()
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }
    }
}
