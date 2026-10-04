using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Four real NGO clients, one repeatedly reconnecting beyond ClientId 4. No cloud/mic needed.
    public sealed class CapPlayerSlotSmoke : MonoBehaviour
    {
        private bool failed;private string role,folder;
        private NetworkManager Manager=>NetworkManager.Singleton;
        private CapNetworkPlayer[] Players=>FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).ToArray();
        private CapNetworkPlayer Local=>Manager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(CapPlayerProfile.Verification || Array.IndexOf(Environment.GetCommandLineArgs(),"--profile-preview")>=0)new GameObject("Slot verification").AddComponent<CapPlayerSlotSmoke>();
#endif
        }
        private void Check(bool ok,string label){if(!ok)failed=true;Debug.Log("[SLOT-VERIFY] "+(ok?"PASS ":"FAIL ")+label);}
        private void Mark(string name){File.WriteAllText(Path.Combine(folder,name),"ready");}
        private bool Has(string name)=>File.Exists(Path.Combine(folder,name));
        private IEnumerator Wait(Func<bool> condition,string label,float seconds=30)
        {float end=Time.realtimeSinceStartup+seconds;while(!condition() && Time.realtimeSinceStartup<end)yield return null;Check(condition(),label);}
        private void Snapshot(string phase)
        {
            var players=Players;
            Check(players.Length==4 && players.Select(p=>p.PlayerSlot.Value).OrderBy(x=>x).SequenceEqual(new[]{0,1,2,3}),phase+" unique P1..P4");
            Check(players.Select(p=>p.ColorIndex.Value).Distinct().Count()==4 && players.All(p=>p.ColorIndex.Value>=0),phase+" unique colors");
            Check(players.All(p=>p.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.enabled && r.color==CapPlayerProfile.GetColor(p.ColorIndex.Value))),phase+" visual colors synchronized");
        }
        private IEnumerator Start()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--profile-preview")>=0)
            {
                yield return new WaitForSecondsRealtime(2);
                CapRelaySession.Instance.StartLocal(true);
                float deadline=Time.realtimeSinceStartup+10;
                while(Local==null && Time.realtimeSinceStartup<deadline)yield return null;
                FindFirstObjectByType<CapLobbyUI>().OpenProfileEditor(Local);
                yield return new WaitForSecondsRealtime(4);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Application.dataPath+"/../profile-options.png");yield break;
            }
            var args=Environment.GetCommandLineArgs();int roleIndex=Array.IndexOf(args,"--slot-role");role=args[roleIndex+1];
            folder=Path.GetFullPath(Application.dataPath+"/../lobby-profile-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            if(role!="host")yield return Wait(()=>Has(role=="one"?"host-ready":role=="two"?"one-ready":"two-ready"),"previous player ready");
            CapRelaySession.Instance.StartLocal(role=="host");
            yield return Wait(()=>Local!=null && Local.PlayerSlot.Value>=0,"connected and slot assigned");
            if(Local==null){Finish();yield break;}
            int originalSlot=Local.PlayerSlot.Value;ulong originalId=Manager.LocalClientId;
            Mark(role+"-ready");
            yield return Wait(()=>Players.Length==4 && Players.All(p=>p.PlayerSlot.Value>=0),"four synchronized players");
            yield return new WaitForSecondsRealtime(.5f);Snapshot("initial");
            var lobby=FindFirstObjectByType<CapLobbyUI>();
            lobby.OpenProfileEditor(Players.First(p=>!p.IsOwner));Check(!CapLobbyUI.ProfileEditing,"cannot edit another player's row");
            lobby.OpenProfileEditor(Local);Check(CapLobbyUI.ProfileEditing && CapControls.Blocked && !CapOptions.IsOpen,"own row opens inline editor and blocks game keys");
            lobby.CloseProfileEditor();Check(!CapControls.Blocked,"closing inline editor restores input");
            if(role=="host")Mark("profile-race");
            yield return Wait(()=>Has("profile-race"),"profile race begins");
            Local.ApplyProfile("탐정_"+role,4);
            yield return new WaitForSecondsRealtime(1);
            Snapshot("simultaneous profile change");
            Check(Players.Count(p=>p.ColorIndex.Value==4)==1,"only one claimant receives purple");
            Mark(role+"-profile");
            if(role=="host")
            {
                yield return Wait(()=>Has("one-profile")&&Has("two-profile")&&Has("cycle-profile"),"profile checks complete");
                var stable=Players.Where(p=>p.PlayerSlot.Value<3).ToDictionary(p=>p.OwnerClientId,p=>p.PlayerSlot.Value);
                for(int round=0;round<6;round++)
                {
                    if(round==2)CapWarmTown.StartForAll(true);
                    if(round==4)CapWarmTown.StartForAll(false);
                    Mark("leave-"+round);
                    yield return Wait(()=>Players.Length==3,"departure "+round);
                    Check(Players.All(p=>stable.ContainsKey(p.OwnerClientId)&&stable[p.OwnerClientId]==p.PlayerSlot.Value),"remaining numbers do not shift");
                    Mark("join-"+round);
                    yield return Wait(()=>Players.Length==4 && Has("joined-"+round),"rejoin "+round);
                    yield return new WaitForSecondsRealtime(.5f);Snapshot("round "+round);
                    var newcomer=Players.FirstOrDefault(p=>p.PlayerSlot.Value==3);
                    Check(newcomer!=null && newcomer.OwnerClientId>3 && newcomer.Nickname.Value.ToString()=="재입장"+round,"new client ID keeps free seat and custom name");
                    Check(newcomer!=null && newcomer.InTown.Value==(round>=2 && round<4),"correct lobby/town on rejoin");
                    Mark("done-"+round);
                }
                Mark("finished");
                yield return Wait(()=>Has("one-result.txt")&&Has("two-result.txt")&&Has("cycle-result.txt"),"all clients verified");
            }
            else if(role=="cycle")
            {
                for(int round=0;round<6;round++)
                {
                    yield return Wait(()=>Has("leave-"+round),"leave command");
                    CapRelaySession.Instance.LeaveRoom();
                    yield return Wait(()=>!Manager.IsListening,"shutdown complete");
                    yield return Wait(()=>Has("join-"+round),"host released old seat");
                    CapRelaySession.Instance.StartLocal(false);
                    yield return Wait(()=>Local!=null && Local.PlayerSlot.Value>=0,"reconnected");
                    if(Local==null){Finish();yield break;}
                    Check(Local.PlayerSlot.Value==originalSlot && Manager.LocalClientId>originalId,"rejoin reuses free P4 instead of modulo");
                    Local.ApplyProfile("재입장"+round,6);
                    yield return Wait(()=>Local.Nickname.Value.ToString()=="재입장"+round && Local.ColorIndex.Value==6,"name and cyan synchronized");
                    yield return new WaitForSecondsRealtime(.3f);Snapshot("client rejoin "+round);
                    Mark("joined-"+round);yield return Wait(()=>Has("done-"+round),"host checked rejoin");
                }
                yield return Wait(()=>Has("finished"),"test complete");
            }
            else
            {
                yield return Wait(()=>Has("finished"),"all reconnect rounds complete",150);
                Check(Local!=null && Local.PlayerSlot.Value==originalSlot,"stable client retained original number");Snapshot("stable final view");
            }
            Finish();
        }
        private void Finish(){File.WriteAllText(Path.Combine(folder,role+"-result.txt"),failed?"FAILED":"PASSED");CapRelaySession.Instance.LeaveRoom();Application.Quit();}
    }
}
