using System.Collections;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
namespace Cap.Multiplayer {
 public sealed class CapShapeSmoke : MonoBehaviour {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Boot() {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
   if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--shape-verify")>=0)new GameObject("Shape verification").AddComponent<CapShapeSmoke>();
#endif
  }
  private static void Capture(string path) {
   var camera=Camera.main;
   var rt=RenderTexture.GetTemporary(1534,1025,24,RenderTextureFormat.ARGB32);
   var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=rt };
   UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
   var previous=RenderTexture.active;RenderTexture.active=rt;
   var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
   image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
   File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
   RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
  }
  private IEnumerator Start() {
   var nm=NetworkManager.Singleton;yield return new WaitForSecondsRealtime(5);
   if(!nm.IsConnectedClient){Debug.LogError("[SHAPE-VERIFY] Connection failed");Application.Quit(2);yield break;}
   var player=nm.LocalClient.PlayerObject.GetComponent<CapNetworkPlayer>();var town=CapWarmTown.Instance;
   if(nm.IsServer) {
    if(nm.ConnectedClientsIds.Count!=2)Debug.LogError("[SHAPE-VERIFY] Expected two connected players");
    CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(2);
    for(int slot=0;slot<4;slot++){var spawn=CapWarmTown.Spawn(slot);if(town.Blocked(new Vector2(spawn.x-CapWarmTown.OffsetX,spawn.y)))Debug.LogError("[SHAPE-VERIFY] Spawn blocked "+slot);}
    string dir=Application.dataPath+"/../Verification";Directory.CreateDirectory(dir);
    Capture(dir+"/campus.png");
    var before=player.transform.position;CapNetworkPlayer.TestInput=Vector2.down;yield return new WaitForSecondsRealtime(1);CapNetworkPlayer.TestInput=null;
    if(Vector3.Distance(before,player.transform.position)<1)Debug.LogError("[SHAPE-VERIFY] Movement failed");
    var obstacle=town.Obstacles[0];Vector3 start=new Vector3(CapWarmTown.OffsetX+obstacle.center.x,obstacle.yMin-1,0);
    if(town.Move(start,Vector2.up*2).y>=obstacle.yMin)Debug.LogError("[SHAPE-VERIFY] Collision failed");
    town.SetOverviewForVerification(true);yield return new WaitForSecondsRealtime(1);Capture(dir+"/overview.png");
    yield return new WaitForSecondsRealtime(.5f);town.SetOverviewForVerification(false);
    player.GetComponent<NetworkTransform>().Teleport(CapWarmTown.MapWorld(430,700),Quaternion.identity,player.transform.localScale);
    yield return new WaitForSecondsRealtime(1);Capture(dir+"/shops.png");yield return new WaitForSecondsRealtime(1);
    CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(1);
    if(town.InTown)Debug.LogError("[SHAPE-VERIFY] Lobby return failed");
    CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(1);
    if(!town.InTown)Debug.LogError("[SHAPE-VERIFY] Second start failed");
    Debug.Log("[SHAPE-VERIFY] Host checked 2 players, 4 spawn slots, movement, collision, camera captures, lobby return and second start");
   } else {
    bool entered=false,returned=false,reentered=false,hostMoved=false;Vector3 initial=Vector3.zero;
    for(int i=0;i<110;i++) {
     if(town.InTown){if(returned)reentered=true;entered=true;}else if(entered)returned=true;
     foreach(var p in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))if(p.OwnerClientId==0&&p.InTown.Value){if(initial==Vector3.zero)initial=p.transform.position;else if(Vector3.Distance(initial,p.transform.position)>1)hostMoved=true;}
     yield return new WaitForSecondsRealtime(.1f);
    }
    if(!entered||!returned||!reentered||!hostMoved)Debug.LogError($"[SHAPE-VERIFY] Client failed enter={entered} return={returned} reenter={reentered} move={hostMoved}");
    else Debug.Log("[SHAPE-VERIFY] Client received host movement and town/lobby/town transitions");
   }
   yield return new WaitForSecondsRealtime(2);Application.Quit();
  }
 }
}
