using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapLoadingReadySmoke : MonoBehaviour
    {
        private bool host,failed;private string folder;
        private NetworkManager Manager=>NetworkManager.Singleton;
        private CapNetworkPlayer Local=>Manager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        private CapNetworkPlayer[] Players=>FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).ToArray();
        private bool Has(string key)=>File.Exists(folder+"/"+key);
        private void Mark(string key)=>File.WriteAllText(folder+"/"+key,"ready");
        private void Check(bool ok,string label){Debug.Log("[LOADING-READY] "+(ok?"PASS ":"FAIL ")+label);if(!ok)failed=true;}
        private IEnumerator Wait(Func<bool> predicate,string label,float timeout=40)
        {float until=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;Check(predicate(),label);}
        private IEnumerator Reveal(){yield return Wait(()=>!CapLoadingScreen.Blocking,"loading releases controls");}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--loading-ready-verify")>=0)new GameObject("Loading ready verification").AddComponent<CapLoadingReadySmoke>();
#endif
        }
        private IEnumerator Start()
        {
            host=Array.IndexOf(Environment.GetCommandLineArgs(),"--loading-ready-host")>=0;
            folder=Path.GetFullPath(Application.dataPath+"/../host-start-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            var session=CapRelaySession.Instance;
            if(host){session.CreateRoom();Check(CapLoadingScreen.Blocking,"create room immediately shows loading");}
            else{
                yield return Wait(()=>Has("code"),"Relay code available",120);if(!Has("code")){Finish();yield break;}
                session.JoinRoom(File.ReadAllText(folder+"/code"));Check(CapLoadingScreen.Blocking,"join room immediately shows loading");
            }
            yield return Wait(()=>session.Connected&&!session.Busy&&Local!=null,"Relay connected",120);
            if(Local==null){Finish();yield break;}
            if(host){File.WriteAllText(folder+"/code.tmp",session.JoinCode);File.Move(folder+"/code.tmp",folder+"/code");}
            yield return Reveal();yield return Wait(()=>Players.Length==2,"two players present");
            if(host)
            {
                Local.SetReady(true);yield return new WaitForSecondsRealtime(.4f);
                Local.StartReadyCountdown();Check(Local.ReadyToStart.Value && Local.StartAt.Value<0 && !Local.InTown.Value,"one ready player cannot start two-player room");Mark("ready-first");
                yield return Wait(()=>Players.All(p=>p.ReadyToStart.Value),"all players ready");
                yield return new WaitForSecondsRealtime(3.4f);
                Check(!Local.InTown.Value && Local.StartAt.Value<0 && Local.CanStartFromLobby,"all ready waits for host button without auto start");
                Players.First(p=>!p.IsOwner).StartReadyCountdown();Check(Local.StartAt.Value<0,"host cannot start through another player object");
                Local.StartReadyCountdown();yield return Wait(()=>Local.StartAt.Value>0,"host button starts countdown");
                double original=Local.StartAt.Value;Local.StartReadyCountdown();Check(Local.StartAt.Value==original,"repeated start does not restart countdown");
                double first=Local.StartAt.Value;Check(first-Manager.ServerTime.Time>2.7,"full three second deadline");Mark("cancel");
                yield return Wait(()=>Local.StartAt.Value<0,"cancel clears countdown");yield return new WaitForSecondsRealtime(3.2f);
                Check(!Local.InTown.Value,"cancelled countdown never starts later");Mark("ready-leave");
                yield return Wait(()=>Players.All(p=>p.ReadyToStart.Value),"ready again after cancellation");
                Check(Local.StartAt.Value<0,"ready again still requires host button");Local.StartReadyCountdown();
                yield return Wait(()=>Local.StartAt.Value>0,"second countdown starts");Mark("leave");
                yield return Wait(()=>Players.Length==1,"participant leaves during countdown");yield return new WaitForSecondsRealtime(.15f);
                Check(!Local.InTown.Value && Local.StartAt.Value<0 && !Local.ReadyToStart.Value,"roster change cancels countdown and requests readiness again");Mark("rejoin");
                yield return Wait(()=>Players.Length==2,"participant rejoins",60);yield return new WaitForSecondsRealtime(.2f);
                Check(!Local.InTown.Value && Local.StartAt.Value<0,"new unready participant prevents countdown");Local.SetReady(true);Mark("final-ready");
                yield return Wait(()=>Players.All(p=>p.ReadyToStart.Value),"ready after rejoin");Local.StartReadyCountdown();
                yield return Wait(()=>Local.StartAt.Value>0,"final countdown starts");
                double final=Local.StartAt.Value;yield return new WaitForSecondsRealtime(2);
                Check(!Local.InTown.Value,"does not start before three seconds");
                yield return Wait(()=>Local.InTown.Value,"starts automatically after countdown");Check(Manager.ServerTime.Time>=final,"host enforces deadline");
                yield return null;Check(CapLoadingScreen.Blocking && CapControls.Blocked,"town transition covered and input blocked");
                var start=Local.transform.position;CapNetworkPlayer.TestInput=Vector2.right;
                yield return new WaitForSecondsRealtime(.5f);
                Check(CapLoadingScreen.Blocking&&CapLoadingScreen.Instance.Alpha>.95f,"minimum opaque loading duration");
                Check(Vector2.Distance(start,Local.transform.position)<.01f,"movement stopped behind loading");
                yield return new WaitForSecondsRealtime(.3f);Check(CapLoadingScreen.Instance.Alpha>0&&CapLoadingScreen.Instance.Alpha<1,"loading fades out");
                CapNetworkPlayer.TestInput=null;yield return Reveal();Check(!Local.ReadyToStart.Value,"ready resets after start");
                Local.GetComponent<NetworkTransform>().Teleport(CapWarmTown.Instance.StudentDoor,Quaternion.identity,Local.transform.localScale);
                yield return new WaitForSecondsRealtime(.3f);Local.InteractDoor();yield return Wait(()=>Local.InMeetingRoom.Value,"door enters meeting room");
                yield return null;Check(CapLoadingScreen.Blocking,"meeting entry has loading");yield return Reveal();
                Local.GetComponent<NetworkTransform>().Teleport(CapWarmTown.MeetingDoor,Quaternion.identity,Local.transform.localScale);yield return new WaitForSecondsRealtime(.3f);
                Local.InteractDoor();yield return Wait(()=>!Local.InMeetingRoom.Value,"door exits room");yield return null;Check(CapLoadingScreen.Blocking,"meeting exit has loading");yield return Reveal();
                CapWarmTown.StartForAll(false);yield return null;Check(CapLoadingScreen.Blocking,"return to lobby has loading");yield return Reveal();
                Check(Players.All(p=>!p.ReadyToStart.Value)&&Local.StartAt.Value<0,"return to lobby requires fresh readiness");
                CapLoadingScreen.Show("전환 확인 중");Time.timeScale=0;yield return new WaitForSecondsRealtime(1.3f);
                Check(!CapLoadingScreen.Blocking,"loading works with paused game time");Time.timeScale=1;
                Mark("done");yield return Wait(()=>Has("client-result.txt"),"client verified transitions");
            }
            else
            {
                yield return Wait(()=>Has("ready-first"),"first ready command");Local.SetReady(true);
                yield return Wait(()=>Players.All(p=>p.ReadyToStart.Value),"client sees all ready");
                Local.StartReadyCountdown();yield return new WaitForSecondsRealtime(.3f);
                Check(!Local.CanStartFromLobby && CapNetworkPlayer.LobbyHost.StartAt.Value<0,"non-host cannot start countdown");
                yield return Wait(()=>Has("cancel"),"cancel command");
                yield return Wait(()=>CapNetworkPlayer.LobbyHost.StartAt.Value>0,"countdown reaches client");Local.SetReady(false);
                yield return Wait(()=>Has("ready-leave"),"ready for departure");Local.SetReady(true);yield return Wait(()=>Has("leave"),"departure command");
                session.LeaveRoom();Check(CapLoadingScreen.Blocking,"leaving shows loading");yield return Wait(()=>!session.Busy&&!session.Connected,"disconnected");yield return Reveal();
                yield return Wait(()=>Has("rejoin"),"rejoin command");session.JoinRoom(File.ReadAllText(folder+"/code"));
                yield return Wait(()=>Local!=null&&session.Connected&&!session.Busy,"rejoined",90);yield return Reveal();
                Check(!Local.ReadyToStart.Value,"rejoin starts unready");yield return Wait(()=>Has("final-ready"),"final ready command");Local.SetReady(true);
                yield return Wait(()=>Local.InTown.Value,"client starts with host");yield return null;Check(CapLoadingScreen.Blocking,"client also gets town loading");yield return Reveal();
                yield return Wait(()=>!Local.InTown.Value,"host returns all to lobby");yield return null;Check(CapLoadingScreen.Blocking,"client return loading");yield return Reveal();
                yield return Wait(()=>Has("done"),"host finished");
            }
            Finish();
        }
        private void Finish(){Time.timeScale=1;CapNetworkPlayer.TestInput=null;File.WriteAllText(folder+(host?"/host-result.txt":"/client-result.txt"),failed?"FAILED":"PASSED");CapRelaySession.Instance.LeaveRoom();Application.Quit();}
    }
}
