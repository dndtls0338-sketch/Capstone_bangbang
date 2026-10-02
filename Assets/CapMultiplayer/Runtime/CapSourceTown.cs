using UnityEngine;
namespace Cap.Multiplayer {
    public sealed partial class CapWarmTown {
        // Layout uses the original map coordinates, not image assets.
        public const float MapPPU=50f/3.5f;
        public const float ReferenceCameraHalfHeight=10.25f;
        public const float ReferencePlayerHeight=1.8f;
        public static Vector3 MapWorld(float x,float y) => new Vector3(OffsetX+(x-768)/MapPPU,(512-y)/MapPPU,0);
    }
}
