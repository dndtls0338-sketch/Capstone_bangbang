using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Cap.Multiplayer {
 public sealed class CapMapSmoke:MonoBehaviour {
  private bool failed;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
   if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--map-verify")>=0){Application.runInBackground=true;new GameObject("Map verification").AddComponent<CapMapSmoke>();}
#endif
  }
  private void Check(bool condition,string text){if(!condition){failed=true;Debug.LogError("[MAP-VERIFY] FAIL "+text);}else Debug.Log("[MAP-VERIFY] PASS "+text);}
  private IEnumerator Start(){
   yield return new WaitForSecondsRealtime(5);
   var nm=NetworkManager.Singleton;Check(nm.IsConnectedClient,"connected");if(!nm.IsConnectedClient){Application.Quit(2);yield break;}
   var town=CapWarmTown.Instance;var player=nm.LocalClient.PlayerObject.GetComponent<CapNetworkPlayer>();
   string output=Application.dataPath+"/../MapVerification";Directory.CreateDirectory(output);
   if(nm.IsServer){
    Check(nm.ConnectedClientsIds.Count==2,"two players");CapWarmTown.StartForAll(true);
    yield return new WaitForSecondsRealtime(2);CapNetworkPlayer.TestInput=Vector2.down;
    yield return new WaitForSecondsRealtime(2);CapNetworkPlayer.TestInput=null;
    var cam=Camera.main;float zoom=cam.orthographicSize;Vector3 cameraPosition=cam.transform.position;
    town.SetMapOpen(true);yield return new WaitForSecondsRealtime(.3f);
    var position=player.transform.position;CapNetworkPlayer.TestInput=Vector2.right;
    yield return new WaitForSecondsRealtime(1.5f);
    Check(player.ViewingMap.Value && Vector3.Distance(position,player.transform.position)<.01f,"host cannot move while map open");
    Check(cam.rect==new Rect(0,0,1,1),"viewport fills 16:9 Game view");
    Check(Mathf.Abs(cam.orthographicSize-zoom)<.001f && Vector3.Distance(cam.transform.position,cameraPosition)<.1f,"game camera does not zoom or move when map opens");
    Check(!FindFirstObjectByType<CapLobbyUI>().enabled,"lobby UI disabled in town");
    Check(town.MapImage!=null,"static map image exists");File.WriteAllBytes(output+"/map-background.png",town.MapImage.EncodeToPNG());
    Screen.SetResolution(1280,1024,false);yield return new WaitForSecondsRealtime(.5f);
    Check(cam.rect==new Rect(0,0,1,1),"viewport still fills resized Game view");
    CapNetworkPlayer.TestInput=null;town.SetMapOpen(false);yield return new WaitForSecondsRealtime(.3f);
    var start=player.transform.position;CapNetworkPlayer.TestInput=Vector2.right;yield return new WaitForSecondsRealtime(1);
    CapNetworkPlayer.TestInput=null;Check(Vector3.Distance(start,player.transform.position)>1,"host resumes after closing map");
    CaptureWorld(output+"/fullscreen-world.png");
    town.SetMapOpen(true);CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(1);
    Check(!town.Overview&&!player.ViewingMap.Value && FindFirstObjectByType<CapLobbyUI>().enabled,"lobby return resets map and restores lobby UI");
    CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(1);
    Check(town.InTown&&!town.Overview&&!player.ViewingMap.Value,"second entry starts with map closed");
    yield return new WaitForSecondsRealtime(3);
   }else{
    float deadline=Time.unscaledTime+6;while(!town.InTown && Time.unscaledTime<deadline)yield return null;
    Check(town.InTown,"client entered town");town.SetMapOpen(true);yield return new WaitForSecondsRealtime(.3f);
    var host=System.Array.Find(FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None),p=>p.OwnerClientId==0);
    var area=new Rect(0,0,1536,1024);var firstIcon=CapWarmTown.MapIconPosition(host,area);
    var position=player.transform.position;var photo=town.MapImage;var bytes=photo.GetRawTextureData<byte>().ToArray();
    CapNetworkPlayer.TestInput=Vector2.down;yield return new WaitForSecondsRealtime(2.8f);
    Check(player.ViewingMap.Value && Vector3.Distance(position,player.transform.position)<.01f,"client input is blocked by host while map open");
    Check(Vector2.Distance(firstIcon,CapWarmTown.MapIconPosition(host,area))>5,"remote player icon follows live movement");
    Check(photo==town.MapImage && System.Linq.Enumerable.SequenceEqual(bytes,photo.GetRawTextureData<byte>().ToArray()),"map photograph stays unchanged while remote moves");
    Check(Camera.main.rect==new Rect(0,0,1,1),"client viewport fills screen");
    town.SetMapOpen(false);yield return new WaitForSecondsRealtime(.3f);
    position=player.transform.position;yield return new WaitForSecondsRealtime(1.2f);CapNetworkPlayer.TestInput=null;
    Check(Vector3.Distance(position,player.transform.position)>1,"client resumes after closing map");
    deadline=Time.unscaledTime+10;while(town.InTown && Time.unscaledTime<deadline)yield return null;
    Check(!town.Overview&&!player.ViewingMap.Value,"client map reset on lobby return");
    deadline=Time.unscaledTime+5;while(!town.InTown && Time.unscaledTime<deadline)yield return null;
    Check(town.InTown&&!town.Overview,"client reentered town normally");
   }
   string role=nm.IsServer?"host":"client";File.WriteAllText(output+"/"+role+"-result.txt",failed?"FAILED":"PASSED");
   Debug.Log("[MAP-VERIFY] "+role+" "+(failed?"FAILED":"PASSED"));yield return new WaitForSecondsRealtime(1);Application.Quit(failed?2:0);
  }
  private static void CaptureWorld(string path){
   var cam=Camera.main;var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);
   RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
   var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
   image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(image);
  }
 }
}
