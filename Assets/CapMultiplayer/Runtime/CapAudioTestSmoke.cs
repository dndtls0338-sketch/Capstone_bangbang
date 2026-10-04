using System;
using System.Collections;
using System.IO;
using Unity.Services.Vivox;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Opt-in development verification. Never transmits or writes captured microphone samples.
    public sealed class CapAudioTestSmoke : MonoBehaviour
    {
        private bool failed;
        private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--audio-device-verify")>=0 || Array.IndexOf(Environment.GetCommandLineArgs(),"--audio-options-preview")>=0)
                new GameObject("Audio device verification").AddComponent<CapAudioTestSmoke>();
#endif
        }
        private void Check(bool ok,string name){Debug.Log("[AUDIO-VERIFY] "+(ok?"PASS ":"FAIL ")+name);if(!ok)failed=true;}
        private IEnumerator Start()
        {
            folder=Path.GetFullPath(Application.dataPath+"/../audio-verification");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);
            var voice=CapVoiceChat.Instance;var options=CapOptions.Instance;
            options.ShowAudioTest();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--audio-options-preview")>=0)
            {
                yield return new WaitForSecondsRealtime(5);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(folder+"/audio-options.png");
                yield break;
            }
            float deadline=Time.unscaledTime+45;
            while(!voice.CanTestDevices && Time.unscaledTime<deadline)yield return null;
            Check(voice.CanTestDevices,"devices initialized on title screen");
            if(!voice.CanTestDevices){Finish();yield break;}
            Check(!VivoxService.Instance.IsLoggedIn,"local tests do not require voice login");
            // PCM verification checks actual device buffers but stores/logs no audio.
            var native=new CapAudioDeviceTest();
            try{native.StartInput(VivoxService.Instance.EffectiveInputDevice.DeviceID);}
            catch(Exception e){Check(false,"input open: "+e.Message);}
            for(int i=0;i<20 && native.Running;i++)
            {yield return new WaitForSecondsRealtime(.02f);native.Poll();}
            Check(native.BuffersRead>0,"selected microphone supplies PCM buffers");native.Dispose();
            try{native.StartOutput(VivoxService.Instance.EffectiveOutputDevice.DeviceID,0);}
            catch(Exception e){Check(false,"output open: "+e.Message);}
            Check(native.Running,"selected speaker accepts silent verification buffer");
            deadline=Time.unscaledTime+3;
            while(native.Running && Time.unscaledTime<deadline){native.Poll();yield return null;}
            Check(!native.Running,"speaker playback completes and releases device");native.Dispose();
            voice.ToggleMicrophoneTest();
            Check(voice.TestingMicrophone,"microphone start button activates test");
            yield return new WaitForSecondsRealtime(.2f);
            Check(!voice.Transmitting && !VivoxService.Instance.IsLoggedIn,"microphone test is local only");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(folder+"/audio-options.png");
            yield return new WaitForSecondsRealtime(.2f);
            options.SelectTab(2);Check(!voice.TestingMicrophone,"changing tabs stops microphone");
            options.SelectTab(3);voice.ToggleMicrophoneTest();options.Close();
            Check(!voice.TestingMicrophone,"closing options releases microphone");
            options.ShowAudioTest();voice.ToggleMicrophoneTest();
            voice.SelectDevice(true,VivoxService.Instance.ActiveInputDevice.DeviceID);
            Check(!voice.TestingMicrophone,"device selection stops microphone");
            deadline=Time.unscaledTime+10;while(!voice.CanTestDevices && Time.unscaledTime<deadline)yield return null;
            float volume=voice.VoiceVolume;voice.SetVolume(0);voice.ToggleSpeakerTest();
            Check(!voice.TestingSpeaker && voice.DeviceTestStatus.Contains("음량"),"zero volume explains why test cannot be heard");
            voice.SetVolume(volume);
            options.Close();Finish();
        }
        private void Finish(){File.WriteAllText(folder+"/result.txt",failed?"FAILED":"PASSED");Application.Quit();}
    }
}
