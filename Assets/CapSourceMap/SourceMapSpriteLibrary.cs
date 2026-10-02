using UnityEngine;
public sealed class SourceMapSpriteLibrary : ScriptableObject
{
    public string sourcePolicy="Lossless source textures; cropped Sprite sub-assets; no image regeneration or upscaling.";
    public Sprite[] sprites;
}
