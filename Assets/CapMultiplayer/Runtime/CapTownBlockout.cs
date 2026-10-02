using UnityEngine;
namespace Cap.Multiplayer {
    public sealed partial class CapWarmTown {
        private Font blockoutFont;
        private void UpdateBlockoutLabels() { }
        private void ShapeLabel(string value,float x,float y) {
            if(blockoutFont==null) blockoutFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},48);
            blockoutFont.RequestCharactersInTexture(value,48,FontStyle.Normal);
            var go=new GameObject("Label: "+value); go.transform.SetParent(root.transform,false);
            go.transform.position=PixelWorld(x,y);
            var tm=go.AddComponent<TextMesh>();tm.font=blockoutFont;tm.fontSize=48;tm.characterSize=.12f;
            tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=Color.white;tm.text=value;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=blockoutFont.material;renderer.sortingOrder=5000;
        }
    }
}
