namespace SenzeBodycam;

public static class BodycamInterferenceApi
{
    public static bool Trigger(float intensity, float duration)
    {
        var overlay = PatchContext.BodycamOverlay;
        return overlay != null && overlay.TriggerElectricalInterference(intensity, duration);
    }
}
