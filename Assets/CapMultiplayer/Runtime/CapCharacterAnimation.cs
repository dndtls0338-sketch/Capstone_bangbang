using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Atlas views preserve original image bytes. Left directions mirror right artwork.
    [DefaultExecutionOrder(200)]
    public sealed class CapCharacterAnimation : MonoBehaviour
    {
        [Serializable] private class Frame { public int slot,row,frame,x,y,w,h; public float pivotX,ppu; public string sheet; public bool mirror; }
        [Serializable] private class Catalog { public Frame[] frames; }
        private readonly Dictionary<int,Sprite> frames = new Dictionary<int,Sprite>();
        private readonly Dictionary<int,bool> mirrored = new Dictionary<int,bool>();
        private static readonly int[] Rows={2,3,4,3,2,1,0,1};
        private bool wasRiding;
        private float transitionEnd;
        public bool ShowingBicycle { get; private set; }
        private SpriteRenderer view;
        private CapNetworkPlayer player;
        public int CurrentDirection { get; private set; } = 6;
        public int CurrentFrame { get; private set; }
        public void Initialize(CapNetworkPlayer owner, SpriteRenderer renderer, int slot)
        {
            player=owner; view=renderer;
            LoadAtlas("Walk",slot,0);
            LoadAtlas("Bike",slot,100);
            if(frames.Count!=50) throw new InvalidOperationException("Expected 50 walking and bicycle frames; got "+frames.Count);
            float referenceScale=CapWarmTown.ReferencePlayerHeight/frames[0].bounds.size.y;
            view.transform.localScale=new Vector3(referenceScale/owner.transform.localScale.x,referenceScale/owner.transform.localScale.y,1);
            wasRiding=owner.Riding.Value;
            Apply(6,false,0);
        }
        private void LoadAtlas(string folder,int slot,int offset)
        {
            var catalog=Resources.Load<TextAsset>("WarmTown/"+folder+"/catalog");
            if(catalog==null) throw new InvalidOperationException("Walking frame catalog missing");
            foreach(var f in JsonUtility.FromJson<Catalog>(catalog.text).frames)
            {
                if(f.slot!=slot) continue;
                var texture=Resources.Load<Texture2D>("WarmTown/"+folder+"/"+f.sheet);
                var sprite=Sprite.Create(texture,new Rect(f.x,f.y,f.w,f.h),new Vector2(f.pivotX,0),f.ppu,0,SpriteMeshType.FullRect);
                sprite.name=$"P{slot+1}/row{f.row}/frame{f.frame}";
                frames.Add(offset+f.row*5+f.frame,sprite);
                mirrored.Add(offset+f.row*5+f.frame,f.mirror);
            }
        }
        public void Apply(byte direction,bool walking,double time)
        {
            CurrentDirection=direction&7;
            bool riding=player.Riding.Value;
            if(riding!=wasRiding) { wasRiding=riding; transitionEnd=Time.unscaledTime+.24f; }
            float remaining=Mathf.Max(0,transitionEnd-Time.unscaledTime);
            ShowingBicycle=remaining>.12f?!riding:riding;
            CurrentFrame=walking?1+(int)(time*(ShowingBicycle?10:8)%4):0;
            int key=(ShowingBicycle?100:0)+Rows[CurrentDirection]*5+CurrentFrame;
            view.sprite=frames[key];
            view.flipX=(CurrentDirection>=3 && CurrentDirection<=5)^mirrored[key];
            // Brief weight shift when mounting/dismounting; never alter authoritative feet/collision position.
            float shift=remaining>0?Mathf.Sin(remaining/.24f*Mathf.PI):0;
            view.transform.localRotation=Quaternion.Euler(0,0,shift*(view.flipX?-5:5));
        }
        private void LateUpdate()
        {
            if(player==null || !player.IsSpawned) return;
            var state=player.Locomotion.Value;
            Apply((byte)(state&7),(state&8)!=0,player.NetworkManager.ServerTime.Time);
        }
        private void OnDestroy() { foreach(var sprite in frames.Values) Destroy(sprite); }
    }
}
