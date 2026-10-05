using UnityEngine;

namespace Cap.Multiplayer
{
    // Pure listening rule: the ordinary gameplay camera is used, never the map photograph.
    public static class CapVoiceRange
    {
        public const float MaximumVoiceVolume=2f;
        public const float FullVolumeRadius=1.25f;
        public static float FadeRadius(float cameraHalfHeight,float aspect) => cameraHalfHeight*Mathf.Sqrt(1+aspect*aspect)*.5f;
        public static float Gain(Vector3 viewport, float distance, float fullVolumeRadius, float fadeRadius, bool sameSpace, bool inTown)
        {
            if(!sameSpace) return 0;
            if(!inTown) return 1;
            if(viewport.z<=0 || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1) return 0;
            float falloff=1-Mathf.InverseLerp(fullVolumeRadius,fadeRadius,distance);
            float edge=Mathf.Min(viewport.x,1-viewport.x,viewport.y,1-viewport.y);
            // Last 5% of the screen fades to silence to avoid a loud cut at the border.
            return Mathf.Clamp01(falloff)*Mathf.Clamp01(edge/.05f);
        }
        // 100% is unity gain; 200% uses approximately +6 dB, not the SDK's maximum gain.
        public static int Volume(float gain) => Mathf.RoundToInt(Mathf.Clamp(20*Mathf.Log10(Mathf.Clamp(gain,.003f,MaximumVoiceVolume)),-50,6));
    }
}
