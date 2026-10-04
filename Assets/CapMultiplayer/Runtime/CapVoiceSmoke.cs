using System;
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cap.Multiplayer
{
    // Opt-in integration verification: real Relay and Vivox, with microphone transmission forced off.
    public sealed class CapVoiceSmoke : MonoBehaviour
    {
        private bool host,failed;private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--voice-verify")>=0)new GameObject("Voice verification").AddComponent<CapVoiceSmoke>();
#endif
        }
        private void Check(bool ok,string name){Debug.Log("[VOICE-VERIFY] "+(ok?"PASS ":"FAIL ")+name);if(!ok)failed=true;}
        private void Place(CapNetworkPlayer p,Vector3 position)
        {p.GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position,Quaternion.identity,p.transform.localScale);}
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();host=Array.IndexOf(args,"--voice-host")>=0;
            folder=Path.GetFullPath(Application.dataPath+"/../voice-boost-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            var voice=CapVoiceChat.Instance;var session=CapRelaySession.Instance;
            CapOptions.Instance.Open();
            float deviceDeadline=Time.realtimeSinceStartup+45;
            while(!voice.CanSelectDevices && Time.realtimeSinceStartup<deviceDeadline)yield return null;
            Check(voice.CanSelectDevices && !session.Connected && !VivoxService.Instance.IsLoggedIn,"device settings work before entering a room without joining voice");
            var devices=voice.GetDevices(false);
            Check(devices.Count>0,"speaker dropdown populated on title screen");
            Check(devices.Count>0 && devices[0].Id=="Default System Device","Windows automatic output is first in dropdown");
            var selected=devices.FirstOrDefault(d=>d.Selected);
            if(selected!=null)
            {
                voice.SelectDevice(false,selected.Id);
                while(!voice.CanSelectDevices && Time.realtimeSinceStartup<deviceDeadline)yield return null;
                Check(voice.GetDevices(false).Any(d=>d.Selected && d.Id==selected.Id),"speaker can be selected before connection");
            }
            CapOptions.Instance.Close();
            Check(CapVoiceRange.Gain(new Vector3(.5f,.5f,1),1,2.5f,20,true,true)==1,"near full volume");
            Check(CapVoiceRange.Gain(new Vector3(.7f,.5f,1),10,2.5f,20,true,true)<1,"farther quieter");
            Check(CapVoiceRange.Gain(new Vector3(1.01f,.5f,1),3,2.5f,20,true,true)==0,"offscreen silent");
            Check(CapVoiceRange.Gain(new Vector3(.5f,.5f,1),1,2.5f,20,false,false)==0,"different spaces silent");
            if(host)
            {
                var old=CapControls.Get(CapAction.Map);string message;
                Check(!CapControls.Bind(CapAction.Map,Key.Escape,out message),"reserved key rejected");
                Check(!CapControls.Bind(CapAction.Map,CapControls.Get(CapAction.Up),out message),"duplicate key rejected");
                Key unused=Enum.GetValues(typeof(Key)).Cast<Key>().First(k=>CapControls.Allowed(k)&&!Enum.GetValues(typeof(CapAction)).Cast<CapAction>().Any(a=>CapControls.Get(a)==k));
                Check(CapControls.Bind(CapAction.Map,unused,out message)&&PlayerPrefs.GetInt("Cap.Key.Map")== (int)unused,"rebind persists");
                CapControls.Bind(CapAction.Map,old,out message);
                CapOptions.Instance.Open();Check(CapControls.Blocked,"options block gameplay input");CapOptions.Instance.Close();
                session.CreateRoom();
            }
            else
            {
                float deadline=Time.realtimeSinceStartup+120;
                while(!File.Exists(folder+"/code.txt")&&Time.realtimeSinceStartup<deadline)yield return null;
                if(!File.Exists(folder+"/code.txt")){Finish("host did not publish room");yield break;}
                session.JoinRoom(File.ReadAllText(folder+"/code.txt"));
            }
            float until=Time.realtimeSinceStartup+120;
            while((!session.Connected || session.Busy)&&Time.realtimeSinceStartup<until)yield return null;
            if(!session.Connected){Finish("Relay connection failed: "+session.Status);yield break;}
            if(host){File.WriteAllText(folder+"/code.tmp",session.JoinCode);File.Move(folder+"/code.tmp",folder+"/code.txt");}
            until=Time.realtimeSinceStartup+120;
            while((!voice.Ready || FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length<2 || VivoxService.Instance.ActiveChannels.Values.Sum(c=>c.Count)<2)&&Time.realtimeSinceStartup<until)yield return null;
            if(!voice.Ready){Finish("Vivox did not connect: "+voice.Status);yield break;}
            yield return new WaitForSecondsRealtime(2);
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            if(players.Length!=2){Finish("second player missing");yield break;}
            var local=players.First(p=>p.IsOwner);var remote=players.First(p=>!p.IsOwner);
            Check(remote.VoicePlayerId.Value.Length>0,"authenticated voice identity synchronized");
            Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())>.95f,"lobby global voice");
            Check(!voice.Transmitting,"test never transmits microphone");
            float originalVolume=voice.VoiceVolume,originalMaster=AudioListener.volume;
            AudioListener.volume=1;voice.SetVolume(2);
            Check(voice.VoiceVolume==2 && PlayerPrefs.GetFloat("Cap.Voice.Volume")==2,"200 percent voice setting applies and persists");
            yield return new WaitForSecondsRealtime(.3f);
            Check(VivoxService.Instance.OutputDeviceVolume==6,"real Vivox output receives positive gain at 200 percent");
            voice.SetVolume(0);yield return new WaitForSecondsRealtime(.3f);
            Check(VivoxService.Instance.IsOutputDeviceMuted,"zero voice volume remains muted");
            voice.SetVolume(originalVolume);AudioListener.volume=originalMaster;yield return new WaitForSecondsRealtime(.3f);
            File.WriteAllText(folder+(host?"/host-ready":"/client-ready"),"ready");
            if(host)
            {
                until=Time.realtimeSinceStartup+30;while(!File.Exists(folder+"/client-ready")&&Time.realtimeSinceStartup<until)yield return null;
                CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(2);
                var origin=CapWarmTown.Spawn(0);Place(local,origin);Place(remote,origin+Vector3.right*2);
                yield return new WaitForSecondsRealtime(2);Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())>.9f,"near town voice");
                Place(remote,origin+Vector3.right*9);yield return new WaitForSecondsRealtime(2);
                float farther=voice.GainFor(remote.VoicePlayerId.Value.ToString());Check(farther>0&&farther<.9f,"distance attenuation applied to real participant");
                CapWarmTown.Instance.SetOverviewForVerification(true);yield return new WaitForSecondsRealtime(1);
                Check(Mathf.Abs(voice.GainFor(remote.VoicePlayerId.Value.ToString())-farther)<.03f,"map overlay does not expand hearing");CapWarmTown.Instance.SetOverviewForVerification(false);
                float cutoff=CapVoiceRange.FadeRadius(Camera.main.orthographicSize,Camera.main.aspect);
                Place(remote,origin+Vector3.right*(cutoff+.5f));yield return new WaitForSecondsRealtime(1);
                Check(Camera.main.WorldToViewportPoint(remote.VisualCenter).x<1 && voice.GainFor(remote.VoicePlayerId.Value.ToString())==0,"beyond half-radius silent even while still onscreen");
                Place(remote,origin+Vector3.right*50);yield return new WaitForSecondsRealtime(1);
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())==0,"offscreen real participant muted");
                var participant=VivoxService.Instance.ActiveChannels.Values.SelectMany(c=>c).FirstOrDefault(p=>!p.IsSelf);
                Check(participant!=null&&participant.IsMuted,"Vivox local mute active offscreen");
                Place(remote,origin+Vector3.right*2);yield return new WaitForSecondsRealtime(1);
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())>.9f,"reentry restores audible volume");
                remote.SetTown(false);yield return new WaitForSecondsRealtime(1);
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())==0,"different scene muted");
                CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(1);
                Check(voice.GainFor(remote.VoicePlayerId.Value.ToString())>.95f,"return to lobby restores global voice");
                File.WriteAllText(folder+"/done","done");
            }
            else
            {
                until=Time.realtimeSinceStartup+70;while(!File.Exists(folder+"/done")&&Time.realtimeSinceStartup<until)yield return null;
                Check(File.Exists(folder+"/done"),"host range tests completed");
            }
            session.LeaveRoom();yield return new WaitForSecondsRealtime(5);
            Check(!voice.Ready && !voice.Transmitting,"leaving room stops audio");
            File.WriteAllText(folder+(host?"/host-result.txt":"/client-result.txt"),failed?"FAILED":"PASSED");Application.Quit();
        }
        private void Finish(string reason){Check(false,reason);File.WriteAllText(folder+(host?"/host-result.txt":"/client-result.txt"),"FAILED "+reason);CapRelaySession.Instance?.LeaveRoom();Application.Quit();}
    }
}

