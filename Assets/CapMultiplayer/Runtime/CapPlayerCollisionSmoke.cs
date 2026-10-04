using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Four independent clients use the same authoritative movement/RPC path as Relay.
    public sealed class CapPlayerCollisionSmoke : MonoBehaviour
    {
        private int role;private string folder;private bool failed,monitor,entered,exited;private float nextPoll;
        private NetworkManager Manager=>NetworkManager.Singleton;
        private CapNetworkPlayer Local=>Manager.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
        private CapNetworkPlayer[] Players=>FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Where(p=>p.IsSpawned).OrderBy(p=>p.PlayerSlot.Value).ToArray();
        private void Check(bool ok,string label){Debug.Log("[COLLISION-VERIFY] "+(ok?"PASS ":"FAIL ")+label);if(!ok)failed=true;}
        private bool Has(string key)=>File.Exists(folder+"/"+key);
        private void Mark(string key)=>File.WriteAllText(folder+"/"+key,"ready");
        private IEnumerator Wait(Func<bool> predicate,string name,float timeout=30)
        {float until=Time.realtimeSinceStartup+timeout;while(!predicate()&&Time.realtimeSinceStartup<until)yield return null;Check(predicate(),name);}
        private void Connect(bool host){Manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17983);if(host)Manager.StartHost();else Manager.StartClient();}
        private void Place(CapNetworkPlayer player,Vector3 position)=>player.GetComponent<NetworkTransform>().Teleport(position,Quaternion.identity,player.transform.localScale);
        private bool Separate(){var p=Players;for(int i=0;i<p.Length;i++)for(int j=i+1;j<p.Length;j++)if(p[i].SameSpace(p[j])&&CapNetworkPlayer.BodiesOverlap(p[i].transform.position,p[j].transform.position))return false;return true;}
        private void Input(params Vector2[] inputs){File.WriteAllText(folder+"/input",string.Join(",",inputs.SelectMany(v=>new[]{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture),v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)})));}
        private void StopInput()=>Input(Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--collision-verify")>=0)new GameObject("Player collision verification").AddComponent<CapPlayerCollisionSmoke>();
#endif
        }
        private void Update()
        {
            if(folder==null || Local==null || Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.05f;
            try{
                if(Has("input")){var v=File.ReadAllText(folder+"/input").Split(',');if(v.Length==8)CapNetworkPlayer.TestInput=new Vector2(float.Parse(v[role*2],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[role*2+1],System.Globalization.CultureInfo.InvariantCulture));}
                if(!entered && Has("enter-"+role)){entered=true;Local.InteractDoor();}
                if(!exited && Has("exit-"+role)){exited=true;Local.InteractDoor();}
            }catch(IOException){}catch(FormatException){}
        }
        private void LateUpdate(){if(monitor && Manager.IsServer && !Separate()){Check(false,"no pair overlap during motion");monitor=false;}}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();role=int.Parse(args[Array.IndexOf(args,"--collision-role")+1]);
            folder=Path.GetFullPath(Application.dataPath+"/../collision-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            if(role>0)yield return Wait(()=>Has("ready-"+(role-1)),"previous client ready");
            Connect(role==0);yield return Wait(()=>Local!=null&&Local.PlayerSlot.Value>=0,"connected");if(Local==null){Finish();yield break;}
            Check(Local.PlayerSlot.Value==role,"expected player slot");Mark("ready-"+role);
            yield return Wait(()=>Players.Length==4,"four players connected");yield return new WaitForSecondsRealtime(.5f);
            if(role==0)
            {
                Check(Separate(),"lobby spawns separated");var p=Players;
                Place(p[0],new Vector3(-3,-1,0));Place(p[1],new Vector3(3,-1,0));Place(p[2],new Vector3(-5,2.2f,0));Place(p[3],new Vector3(5,2.2f,0));
                monitor=true;Input(Vector2.right,Vector2.left,Vector2.zero,Vector2.zero);yield return new WaitForSecondsRealtime(2);
                StopInput();yield return new WaitForSecondsRealtime(.4f);
                Check(Separate()&&p[0].transform.position.x<p[1].transform.position.x&&p[1].transform.position.x-p[0].transform.position.x<1.5f,"head-on walking stops at visible body boundary");
                Input(new Vector2(1,1),Vector2.zero,Vector2.zero,Vector2.zero);yield return new WaitForSecondsRealtime(2);StopInput();yield return new WaitForSecondsRealtime(.4f);
                Check(p[0].transform.position.x>p[1].transform.position.x && p[0].transform.position.y>p[1].transform.position.y+1.8f,"can slide around a blocked player");
                monitor=false;CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(.5f);Check(Separate(),"town start spawns separated");
                var center=new Vector3(97.4f,1.8f,0);
                Place(p[0],center+Vector3.left*5);Place(p[1],center+Vector3.right*5);Place(p[2],center+Vector3.down*5);Place(p[3],center+Vector3.up*5);
                foreach(var player in p)player.Riding.Value=true;
                monitor=true;Input(Vector2.right,Vector2.left,Vector2.up,Vector2.down);yield return new WaitForSecondsRealtime(2);StopInput();yield return new WaitForSecondsRealtime(.4f);
                Check(Separate()&&p[0].transform.position.x<p[1].transform.position.x&&p[2].transform.position.y<p[3].transform.position.y,"four bicycles cannot tunnel through each other");monitor=false;
                for(int i=0;i<4;i++)
                {
                    Place(p[i],CapWarmTown.Instance.StudentDoor);yield return new WaitForSecondsRealtime(.3f);Mark("enter-"+i);
                    int index=i;yield return Wait(()=>p[index].InMeetingRoom.Value,"player enters occupied meeting room "+i);
                    Check(Separate(),"meeting entry chooses free space "+i);
                }
                for(int i=0;i<4;i++)Place(p[i],CapWarmTown.MeetingCenter+new Vector3(-6+i*4,-4,0));
                monitor=true;Input(Vector2.right,Vector2.left,Vector2.right,Vector2.left);yield return new WaitForSecondsRealtime(1);StopInput();yield return new WaitForSecondsRealtime(.4f);
                Check(Separate(),"meeting room movement collision");monitor=false;
                for(int i=0;i<4;i++)
                {
                    Place(p[i],CapWarmTown.MeetingDoor);yield return new WaitForSecondsRealtime(.7f);Mark("exit-"+i);
                    int index=i;yield return Wait(()=>!p[index].InMeetingRoom.Value,"player exits into occupied doorway "+i);
                    Check(Separate(),"exit finds free position "+i);
                }
                CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(.5f);Check(Separate(),"return to lobby separated");
                Mark("rejoin-leave");yield return Wait(()=>Players.Length==3,"fourth player leaves");
                Place(p[0],new Vector3(3,-1,0));Mark("rejoin-now");yield return Wait(()=>Players.Length==4&&Has("rejoined"),"fourth player rejoins");
                Check(Separate(),"rejoin finds free position when original seat is occupied");Mark("done");
                yield return Wait(()=>Has("1-result.txt")&&Has("2-result.txt")&&Has("3-result.txt"),"clients verified final state");
            }
            else
            {
                if(role==3){
                    yield return Wait(()=>Has("rejoin-leave"),"rejoin phase",120);CapRelaySession.Instance.LeaveRoom();
                    yield return Wait(()=>!Manager.IsListening,"client disconnected");yield return Wait(()=>Has("rejoin-now"),"reconnect command");
                    Connect(false);yield return Wait(()=>Local!=null&&Local.PlayerSlot.Value==3,"reconnected with original seat");Mark("rejoined");
                }
                yield return Wait(()=>Has("done"),"host test completed",150);yield return new WaitForSecondsRealtime(.6f);
                Check(Separate(),"client sees separated final positions");Check(entered&&exited,"owner entry and exit RPCs used");
            }
            Finish();
        }
        private void Finish(){CapNetworkPlayer.TestInput=null;File.WriteAllText(folder+"/"+role+"-result.txt",failed?"FAILED":"PASSED");CapRelaySession.Instance.LeaveRoom();Application.Quit();}
    }
}
