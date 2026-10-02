using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        private Texture2D mapImage,mapDot;
        public Texture2D MapImage => mapImage;
        public static Color PlayerColor(ulong id)
        {
            Color[] colors={new Color(1,.65f,.28f),new Color(.3f,.7f,1),new Color(.45f,.9f,.6f),new Color(.95f,.45f,.7f)};
            return colors[(int)(id%4)];
        }

        public void SetMapOpen(bool open)
        {
            var obj=NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var player=obj!=null?obj.GetComponent<CapNetworkPlayer>():null;
            open=open && InTown && player!=null && player.IsSpawned;
            if(open && mapImage==null)CreateMapImage();
            Overview=open;
            if(player!=null)player.SetMapViewing(open);
        }

        // A one-time photo of the environment, with all players excluded.
        // It uses a separate off-screen camera and never zooms the gameplay camera.
        private void CreateMapImage()
        {
            var hidden=new List<SpriteRenderer>();
            var labelScales=new Dictionary<Transform,Vector3>();
            var captureObject=new GameObject("Map photo camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=captureObject.AddComponent<Camera>();camera.CopyFrom(view);camera.enabled=false;
            camera.rect=new Rect(0,0,1,1);camera.orthographic=true;
            camera.orthographicSize=Bounds.height/2;camera.aspect=Bounds.width/Bounds.height;
            camera.transform.position=new Vector3(OffsetX,0,-10);camera.transform.rotation=Quaternion.identity;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.20f);
            var rt=RenderTexture.GetTemporary(1536,1024,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                foreach(var player in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    foreach(var sr in player.GetComponentsInChildren<SpriteRenderer>())
                        if(sr.enabled){hidden.Add(sr);sr.enabled=false;}
                // Map labels must remain readable when the whole village is fitted on screen.
                foreach(var text in root.GetComponentsInChildren<TextMesh>())
                {labelScales.Add(text.transform,text.transform.localScale);text.transform.localScale*=2.2f;}
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                mapImage=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false){name="Static village map",filterMode=FilterMode.Bilinear};
                mapImage.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);mapImage.Apply();
            }
            finally
            {
                foreach(var sr in hidden)if(sr!=null)sr.enabled=true;
                foreach(var pair in labelScales)if(pair.Key!=null)pair.Key.localScale=pair.Value;
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(captureObject);
            }
        }

        public static Vector2 MapIconPosition(CapNetworkPlayer player,Rect mapRect)
        {
            var position=player.RenderPosition;
            float x=Mathf.InverseLerp(Bounds.xMin,Bounds.xMax,position.x-OffsetX);
            float y=1-Mathf.InverseLerp(Bounds.yMin,Bounds.yMax,position.y);
            return new Vector2(mapRect.x+x*mapRect.width,mapRect.y+y*mapRect.height);
        }

        private void DrawMapOverlay()
        {
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;
            GUI.matrix=Matrix4x4.identity;GUI.depth=-100;
            try
            {
                // Fully opaque: no lobby or moving world is visible behind the map.
                GUI.color=new Color(.07f,.10f,.14f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
                GUI.color=Color.white;
                float scale=Mathf.Clamp(Screen.height/900f,.55f,1.5f);
                var heading=new GUIStyle(label){fontSize=Mathf.RoundToInt(26*scale),fontStyle=FontStyle.Bold};
                var hint=new GUIStyle(label){fontSize=Mathf.RoundToInt(16*scale)};
                float margin=24*scale,top=76*scale,bottom=40*scale;
                GUI.Label(new Rect(margin,14*scale,Screen.width-180*scale,40*scale),"마을 지도",heading);
                if(GUI.Button(new Rect(Screen.width-margin-150*scale,16*scale,150*scale,36*scale),"닫기 · M / ESC",new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(16*scale)}))SetMapOpen(false);
                var available=new Rect(margin,top,Screen.width-margin*2,Screen.height-top-bottom);
                float fit=Mathf.Min(available.width/1536,available.height/1024);
                var area=new Rect(available.center.x-1536*fit/2,available.center.y-1024*fit/2,1536*fit,1024*fit);
                if(mapImage!=null)GUI.DrawTexture(area,mapImage,ScaleMode.StretchToFill);
                EnsureMapDot();
                var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
                // Local marker is drawn last so it remains visible at overlapping positions.
                foreach(var player in players)if(!player.IsOwner)DrawMapMarker(player,area,scale);
                foreach(var player in players)if(player.IsOwner)DrawMapMarker(player,area,scale);
                GUI.color=Color.white;
                GUI.Label(new Rect(margin,Screen.height-bottom+6*scale,Screen.width-margin*2,bottom),"지도 확인 중에는 이동할 수 없습니다. 친구들의 위치는 실시간으로 표시됩니다.",hint);
            }
            finally {GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;}
        }

        private void EnsureMapDot()
        {
            if(mapDot!=null)return;
            mapDot=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Map position dot",filterMode=FilterMode.Bilinear};
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)mapDot.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(15.5f-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f)))));
            mapDot.Apply();
        }

        private void DrawMapMarker(CapNetworkPlayer player,Rect area,float scale)
        {
            if(!player.IsSpawned||!player.InTown.Value)return;
            var p=MapIconPosition(player,area);float diameter=(player.IsOwner?22:16)*scale;
            GUI.color=player.IsOwner?Color.white:new Color(.07f,.10f,.14f);
            GUI.DrawTexture(new Rect(p.x-diameter/2-3*scale,p.y-diameter/2-3*scale,diameter+6*scale,diameter+6*scale),mapDot);
            GUI.color=PlayerColor(player.OwnerClientId);
            GUI.DrawTexture(new Rect(p.x-diameter/2,p.y-diameter/2,diameter,diameter),mapDot);
            GUI.color=Color.white;
            var textStyle=new GUIStyle(label){fontSize=Mathf.RoundToInt(15*scale),fontStyle=FontStyle.Bold};
            string name="P"+(player.OwnerClientId%4+1)+(player.IsOwner?" · 나":"");
            float width=82*scale;
            var textRect=new Rect(Mathf.Clamp(p.x+diameter/2+5*scale,area.x,area.xMax-width),Mathf.Clamp(p.y-13*scale,area.y,area.yMax-26*scale),width,26*scale);
            GUI.color=new Color(.07f,.10f,.14f,.9f);GUI.DrawTexture(textRect,Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(textRect,name,textStyle);
        }
    }
}
