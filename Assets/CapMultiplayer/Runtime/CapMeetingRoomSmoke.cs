using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Opt-in, two-process Relay/Vivox verification. CapVoiceChat disables microphone transmission.
    public sealed class CapMeetingRoomSmoke : MonoBehaviour
    {
        private bool host,failed;private string folder;
        private NetworkManager Manager=>NetworkManager.Singleton;
        private CapNetworkPlayer Local=>Manager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        private CapNetworkPlayer[] Players=>FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).ToArray();
        private void Check(bool ok,string label){Debug.Log("[MEETING-VERIFY] "+(ok?"PASS ":"FAIL ")+label);if(!ok)failed=true;}
        private void Mark(string name)=>File.WriteAllText(folder+"/"+name,"ready");
        private bool Has(string name)=>File.Exists(folder+"/"+name);
        private IEnumerator Wait(Func<bool> predicate,string label,float seconds=40)
        {float until=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;Check(predicate(),label);}
        private void Place(CapNetworkPlayer player,Vector3 p)=>player.GetComponent<NetworkTransform>().Teleport(p,Quaternion.identity,player.transform.localScale);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--meeting-verify")>=0)new GameObject("Meeting room verification").AddComponent<CapMeetingRoomSmoke>();
#endif
        }
        private IEnumerator Capture(string name)
        {
            // Hidden Windows builds can have a black backbuffer; explicitly render the gameplay camera.
            yield return new WaitForEndOfFrame();
            var go=new GameObject("Verification camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);
            camera.transform.SetPositionAndRotation(Camera.main.transform.position,Camera.main.transform.rotation);camera.enabled=false;
            var rt=RenderTexture.GetTemporary(1280,800,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try{
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());Destroy(image);
            }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(go);}
        }
        private IEnumerator Start()
        {
            host=Array.IndexOf(Environment.GetCommandLineArgs(),"--meeting-host")>=0;
            folder=Path.GetFullPath(Application.dataPath+"/../meeting-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            var session=CapRelaySession.Instance;var voice=CapVoiceChat.Instance;var town=CapWarmTown.Instance;
            if(host)session.CreateRoom();
            else{yield return Wait(()=>Has("code"),"host publishes Relay code",120);if(!Has("code")){Finish();yield break;}session.JoinRoom(File.ReadAllText(folder+"/code"));}
            yield return Wait(()=>session.Connected&&!session.Busy&&Local!=null,"Relay connected",120);
            if(Local==null){Finish();yield break;}
            if(host){File.WriteAllText(folder+"/code.tmp",session.JoinCode);File.Move(folder+"/code.tmp",folder+"/code");}
            yield return Wait(()=>Players.Length==2&&voice.Ready,"two players and Vivox ready",120);
            if(Players.Length!=2){Finish();yield break;}
            yield return new WaitForSecondsRealtime(2);
            Check(!voice.Transmitting,"no microphone transmission in verification");
            if(host)
            {
                var remote=Players.First(p=>!p.IsOwner);
                CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(1);
                Local.InteractDoor();yield return new WaitForSecondsRealtime(.5f);
                Check(!Local.InMeetingRoom.Value,"E away from door does not enter");
                Check(!town.Blocked(new Vector2(town.StudentDoor.x-CapWarmTown.OffsetX,town.StudentDoor.y)),"door approach is walkable");
                Place(Local,town.StudentDoor);Place(remote,town.StudentDoor+Vector3.right*3);
                yield return new WaitForSecondsRealtime(.5f);yield return Capture("entrance");
                town.SetMapOpen(true);Local.InteractDoor();yield return new WaitForSecondsRealtime(.3f);
                Check(!Local.InMeetingRoom.Value,"map blocks door interaction");town.SetMapOpen(false);
                Local.ToggleBicycle();yield return new WaitForSecondsRealtime(.3f);Local.InteractDoor();
                yield return Wait(()=>Local.InMeetingRoom.Value,"host enters room");
                yield return new WaitForSecondsRealtime(1);
                Check(!Local.Riding.Value && !remote.InMeetingRoom.Value,"entering dismounts only this player");
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())==0,"outside voice muted inside room");
                Check(!remote.GetComponentsInChildren<SpriteRenderer>().Any(s=>s.enabled),"outside player hidden from room");
                Check((Local.transform.position-CapWarmTown.MeetingSpawn).sqrMagnitude<.1f,"room spawn position correct");
                // Actual host movement is stopped by the table, not merely by a visual collider.
                Place(Local,CapWarmTown.MeetingCenter+new Vector3(0,-4,0));CapNetworkPlayer.TestInput=Vector2.up;
                yield return new WaitForSecondsRealtime(1);CapNetworkPlayer.TestInput=null;yield return new WaitForSecondsRealtime(.3f);
                Check(Local.transform.position.y<-1.5f && Local.transform.position.y>-2,"server movement stops at table");
                var beyond=town.MoveInMeetingRoom(CapWarmTown.MeetingCenter+new Vector3(14,4,0),Vector2.right*100);
                Check(beyond.x<315 && beyond.x>314,"room wall blocks movement");
                Place(remote,town.StudentDoor);Mark("client-enter");
                yield return Wait(()=>remote.InMeetingRoom.Value&&Has("client-inside"),"remote owner enters same room");
                Place(Local,CapWarmTown.MeetingCenter+new Vector3(-6,-3,0));Place(remote,CapWarmTown.MeetingCenter+new Vector3(7,3,0));
                yield return new WaitForSecondsRealtime(2);
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())>.95f,"room voice is global despite distance");
                Check(remote.GetComponentsInChildren<SpriteRenderer>().Any(s=>s.enabled),"room peers visible");
                yield return Capture("meeting-room");Mark("together");
                yield return Wait(()=>Has("client-checked"),"client verified synchronized room");
                Place(remote,CapWarmTown.MeetingDoor);yield return new WaitForSecondsRealtime(.5f);Mark("client-exit");
                yield return Wait(()=>!remote.InMeetingRoom.Value&&Has("client-outside"),"remote exits independently");
                Check(Local.InMeetingRoom.Value,"other player exit does not move host");
                Place(Local,CapWarmTown.MeetingDoor);yield return new WaitForSecondsRealtime(.7f);Local.InteractDoor();
                yield return Wait(()=>!Local.InMeetingRoom.Value,"host exits through room door");
                Check(Vector2.Distance(Local.transform.position,town.StudentDoor)<1,"returns to student union door");
                yield return new WaitForSecondsRealtime(.7f);Local.InteractDoor();yield return Wait(()=>Local.InMeetingRoom.Value,"can reenter");
                CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(.5f);
                Check(Players.All(p=>!p.InTown.Value&&!p.InMeetingRoom.Value),"return to lobby clears room state for all");
                Mark("done");yield return Wait(()=>Has("client-result.txt"),"client finished");
            }
            else
            {
                yield return Wait(()=>Has("client-enter"),"entry command",90);yield return new WaitForSecondsRealtime(.3f);Local.InteractDoor();
                yield return Wait(()=>Local.InMeetingRoom.Value,"client enters via owner RPC");
                town.SetMapOpen(true);Local.ToggleBicycle();yield return new WaitForSecondsRealtime(.4f);
                Check(!town.Overview&&!Local.ViewingMap.Value&&!Local.Riding.Value,"room disables village map and bicycle");Mark("client-inside");
                yield return Wait(()=>Has("together"),"both players in room");
                Check(Players.All(p=>p.InMeetingRoom.Value)&&town.IsMeetingRoom,"room state synchronized on client");
                Check(voice.GainFor(Players.First(p=>!p.IsOwner).VoicePlayerId.Value.ToString())>.95f,"client hears full room voice");Mark("client-checked");
                yield return Wait(()=>Has("client-exit"),"exit command");Local.InteractDoor();yield return Wait(()=>!Local.InMeetingRoom.Value,"client exits via owner RPC");Mark("client-outside");
                yield return Wait(()=>Has("done"),"host completed");
            }
            Finish();
        }
        private void Finish(){CapNetworkPlayer.TestInput=null;File.WriteAllText(folder+(host?"/host-result.txt":"/client-result.txt"),failed?"FAILED":"PASSED");CapRelaySession.Instance.LeaveRoom();Application.Quit();}
    }
}
