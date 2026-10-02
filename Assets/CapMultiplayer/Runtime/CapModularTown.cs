using System.Collections.Generic;
using UnityEngine;
namespace Cap.Multiplayer {
    public sealed partial class CapWarmTown {
        public readonly Dictionary<string,SpriteRenderer> ModularObjects=new Dictionary<string,SpriteRenderer>();
        public int GroundSurfaceCount { get; private set; }
        private void Build() { BuildShapeTown(); }
    }
}
