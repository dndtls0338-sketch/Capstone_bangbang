using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed partial class CapWarmTown
    {
        private const int MinimapResolution=224;
        private const float MinimapRadius=18;
        private const float MinimapRefreshSeconds=.075f;
        private Texture2D minimapImage;
        private Color32[] minimapPixels,mapPhotoPixels;
        private float nextMinimapRefresh;
        private bool minimapRoom;
        private const float MinimapPixelRadius=MinimapResolution/2f-7;
        private static readonly Color32 MinimapNavy=new Color32(13,31,51,255);

        private void ResetMinimap()
        {
            nextMinimapRefresh=0;
        }

        // The background photograph is captured once. Only a small circular texture is
        // updated locally; no extra per-frame camera or network messages are needed.
        private void UpdateMinimap(CapNetworkPlayer local)
        {
            if(local==null || !local.IsSpawned)return;
            bool room=local.InMeetingRoom.Value;
            if(minimapRoom!=room){ResetMinimap();minimapRoom=room;}
            float now=Time.unscaledTime;
            if(now<nextMinimapRefresh)return;
            nextMinimapRefresh=now+MinimapRefreshSeconds;
            if(!room && mapImage==null)CreateMapImage();
            if(!room && mapPhotoPixels==null && mapImage!=null)mapPhotoPixels=mapImage.GetPixels32();
            if(minimapImage==null)
            {
                minimapImage=new Texture2D(MinimapResolution,MinimapResolution,TextureFormat.RGBA32,false)
                    {name="Circular live minimap",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                minimapPixels=new Color32[MinimapResolution*MinimapResolution];
            }
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            Vector2 center=local.RenderPosition;
            float worldRadius=room?12:MinimapRadius;
            for(int y=0;y<MinimapResolution;y++)for(int x=0;x<MinimapResolution;x++)
            {
                var offset=new Vector2(x+.5f-MinimapResolution/2f,y+.5f-MinimapResolution/2f);
                float distance=offset.magnitude;
                Color32 color;
                if(distance>MinimapResolution/2f)color=new Color32(0,0,0,0);
                else if(distance>MinimapPixelRadius+4)color=MinimapNavy;
                else if(distance>MinimapPixelRadius+2)color=new Color32(235,239,222,255);
                else if(distance>MinimapPixelRadius)color=new Color32(37,77,105,255);
                else color=MinimapGround(center+offset*(worldRadius/MinimapPixelRadius),room);
                color.a=(byte)(color.a*Mathf.Clamp01(MinimapResolution/2f-distance));
                minimapPixels[y*MinimapResolution+x]=color;
            }
            // Draw the local marker last, with a white outline and a facing indicator.
            foreach(var player in players)if(player.IsSpawned && player!=local && local.SameSpace(player) && player.NetworkManager==local.NetworkManager)
                DrawMinimapMarker(player,center,worldRadius,false);
            DrawMinimapMarker(local,center,worldRadius,true);
            minimapImage.SetPixels32(minimapPixels);minimapImage.Apply(false);
        }

        private Color32 MinimapGround(Vector2 world,bool room)
        {
            if(room)
            {
                Vector2 p=world-(Vector2)MeetingCenter;
                if(MeetingTable.Contains(p))return new Color32(148,99,64,255);
                if(MeetingBounds.Contains(p))return new Color32(199,191,166,255);
                if(new Rect(-15.8f,-10.8f,31.6f,21.6f).Contains(p))return new Color32(82,99,120,255);
                return MinimapNavy;
            }
            Vector2 town=new Vector2(world.x-OffsetX,world.y);
            if(!Bounds.Contains(town) || mapPhotoPixels==null)return MinimapNavy;
            int x=Mathf.Clamp(Mathf.FloorToInt((town.x-Bounds.xMin)/Bounds.width*mapImage.width),0,mapImage.width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt((town.y-Bounds.yMin)/Bounds.height*mapImage.height),0,mapImage.height-1);
            return mapPhotoPixels[y*mapImage.width+x];
        }

        private static Vector2 MinimapPoint(Vector2 world,Vector2 center,float radius) =>
            Vector2.one*(MinimapResolution/2f)+(world-center)*(MinimapPixelRadius/radius);

        private void DrawMinimapMarker(CapNetworkPlayer player,Vector2 center,float radius,bool local)
        {
            var p=MinimapPoint(player.RenderPosition,center,radius);
            Color tint=PlayerColor(player.ColorIndex.Value);
            if(local)
            {
                float angle=(player.Locomotion.Value&7)*Mathf.PI/4;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                DrawMinimapLine(p,p+direction*12,Color.white,1,2);
            }
            DrawMinimapDot(p,local?6:4.5f,local?Color.white:(Color)MinimapNavy,1);
            DrawMinimapDot(p,local?4:3,tint,1);
        }

        private void DrawMinimapLine(Vector2 a,Vector2 b,Color color,float alpha,float thickness=1.2f)
        {
            int steps=Mathf.CeilToInt(Vector2.Distance(a,b));
            for(int i=0;i<=steps;i++)DrawMinimapDot(Vector2.Lerp(a,b,steps==0?0:(float)i/steps),thickness,color,alpha);
        }

        private void DrawMinimapDot(Vector2 p,float radius,Color color,float alpha)
        {
            int left=Mathf.Max(0,Mathf.FloorToInt(p.x-radius)),right=Mathf.Min(MinimapResolution-1,Mathf.CeilToInt(p.x+radius));
            int bottom=Mathf.Max(0,Mathf.FloorToInt(p.y-radius)),top=Mathf.Min(MinimapResolution-1,Mathf.CeilToInt(p.y+radius));
            for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)
            {
                var pixel=new Vector2(x+.5f,y+.5f);
                if((pixel-Vector2.one*(MinimapResolution/2f)).sqrMagnitude>MinimapPixelRadius*MinimapPixelRadius)continue;
                float coverage=Mathf.Clamp01(radius+.5f-Vector2.Distance(pixel,p))*alpha;
                int index=y*MinimapResolution+x;
                minimapPixels[index]=Color.Lerp(minimapPixels[index],color,coverage);
            }
        }

        private void DrawMinimap()
        {
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            float size=200*scale,margin=20*scale;
            var area=new Rect(Screen.width-margin-size,margin,size,size);
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;
            try
            {
                GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
                if(minimapImage!=null)GUI.DrawTexture(area,minimapImage);
                var compass=new GUIStyle(label){fontSize=Mathf.RoundToInt(15*scale),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
                GUI.Label(new Rect(area.x,area.y+8*scale,size,24*scale),"N",compass);
                var caption=new GUIStyle(compass){fontSize=Mathf.RoundToInt(13*scale),fontStyle=FontStyle.Normal};
                var textArea=new Rect(area.x,area.yMax+5*scale,size,24*scale);
                GUI.color=new Color(.04f,.09f,.15f,.9f);GUI.DrawTexture(textArea,Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(textArea,minimapRoom?"회의실":"마을",caption);
                if(Unity.Netcode.NetworkManager.Singleton.IsHost)
                {
                    var button=new GUIStyle(GUI.skin.button){font=font,fontSize=Mathf.RoundToInt(14*scale)};
                    if(GUI.Button(new Rect(area.x,area.yMax+35*scale,size,34*scale),"대기실로 돌아가기",button))StartForAll(false);
                }
            }
            finally{GUI.matrix=oldMatrix;GUI.color=oldColor;}
        }
    }
}
