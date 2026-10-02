using System.Collections;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Cap.Multiplayer;

public sealed class SourceMapVerification : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--source-map-verify")>=0)new GameObject("Source map verification").AddComponent<SourceMapVerification>();}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(6);
        var nm=NetworkManager.Singleton;
        if(nm==null||!nm.IsHost){Debug.LogError("[SOURCE-VERIFY] Host unavailable");Application.Quit(2);yield break;}
        CapWarmTown.StartForAll(true);
        yield return new WaitForSecondsRealtime(2);
        var town=CapWarmTown.Instance;var obj=nm.LocalClient.PlayerObject;
        var player=obj.GetComponent<CapNetworkPlayer>();
        var renderedPlayer=obj.GetComponentInChildren<CapCharacterAnimation>().GetComponent<SpriteRenderer>();
        float playerPixels=renderedPlayer.bounds.size.y/(Camera.main.orthographicSize*2)*1025;
        if(Mathf.Abs(playerPixels-90)>.75f)Debug.LogError("[SOURCE-VERIFY] Player scale mismatch "+playerPixels);
        else Debug.Log("[SOURCE-VERIFY] Player height at reference resolution="+playerPixels);
        string folder=Application.dataPath+"/../Verification";Directory.CreateDirectory(folder);
        string[] names={"01-campus","02-convenience","03-park","04-hospital"};
        Vector2[] points={new Vector2(1120,365),new Vector2(390,708),new Vector2(1143,918),new Vector2(531,246)};
        for(int i=0;i<points.Length;i++)
        {
            var p=CapWarmTown.MapWorld(points[i].x,points[i].y);
            obj.GetComponent<NetworkTransform>().Teleport(p,Quaternion.identity,obj.transform.localScale);
            yield return new WaitForSecondsRealtime(.6f);
            var center=Camera.main.WorldToViewportPoint(player.VisualCenter);
            if(Vector2.Distance(center,new Vector2(.5f,.5f))>.001f)Debug.LogError("[SOURCE-VERIFY] Center failure "+center);
            if(town.Blocked(new Vector2(p.x-CapWarmTown.OffsetX,p.y)))Debug.LogError("[SOURCE-VERIFY] Landmark spawn blocked "+names[i]);
            Capture(folder+"/"+names[i]+".png");
            if(i==1)
            {
                var shop=town.ModularObjects["Convenience store"];
                shop.enabled=false;yield return null;Capture(folder+"/06-independent-building-hidden.png");shop.enabled=true;
            }
        }
        town.SetOverviewForVerification(true);yield return new WaitForSecondsRealtime(.5f);Capture(folder+"/05-overview.png");
        town.SetOverviewForVerification(false);
        obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.MapWorld(730,610),Quaternion.identity,obj.transform.localScale);
        yield return new WaitForSecondsRealtime(.5f);var start=obj.transform.position;
        CapWalkSmoke.InputOverride=Vector2.down;yield return new WaitForSecondsRealtime(1);CapWalkSmoke.InputOverride=Vector2.zero;
        float distance=Vector3.Distance(start,obj.transform.position);
        if(distance<4)Debug.LogError("[SOURCE-VERIFY] Movement blocked "+distance);
        Debug.Log("[SOURCE-VERIFY] Camera centered; fixed reference framing; movement="+distance+"; objects="+town.ModularObjects.Count);
        Debug.Log("[SOURCE-VERIFY] Connected players="+nm.ConnectedClientsIds.Count);
        CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(.5f);
        if(town.InTown)Debug.LogError("[SOURCE-VERIFY] Lobby return failed");
        Debug.Log("[SOURCE-VERIFY] Finished");Application.Quit();
    }
    static void Capture(string path)
    {
        var camera=Camera.main;var oldRect=camera.rect;camera.rect=new Rect(0,0,1,1);
        var rt=RenderTexture.GetTemporary(1534,1025,24,RenderTextureFormat.ARGB32);
        var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt};
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
        var old=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        Destroy(image);RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);camera.rect=oldRect;
    }
}
