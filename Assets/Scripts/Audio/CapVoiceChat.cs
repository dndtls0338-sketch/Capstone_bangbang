using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;

namespace Cap.Multiplayer
{
    // Relay carries game state. Vivox carries microphone audio in a room-specific channel.
    [DefaultExecutionOrder(400)]
    public sealed class CapVoiceChat : MonoBehaviour
    {
        public static CapVoiceChat Instance { get; private set; }
        public string Status { get; private set; } = "";
        public bool Ready { get; private set; }
        public bool VoiceEnabled { get; private set; }
        public bool PushToTalk { get; private set; }
        public bool Muted { get; private set; }
        public float VoiceVolume { get; private set; }
        public bool Transmitting { get; private set; }
        private bool initialized, working, disposed, reconnectRequested;
        private bool verification;
        private bool recovering, resetRequired;
        private readonly CapAudioDeviceTest deviceTest = new CapAudioDeviceTest();
        private float testDeadline, lastInputBufferAt;
        private string testEndpoint;
        public string DeviceTestStatus { get; private set; } = "테스트 버튼을 누르고 장치를 확인하세요.";
        public float MicrophoneLevel { get; private set; }
        public bool TestingMicrophone => deviceTest.Running && deviceTest.IsInput;
        public bool TestingSpeaker => deviceTest.Running && !deviceTest.IsInput;
        public bool CanTestDevices => CanSelectDevices && !working && !recovering && !resetRequired;
        private readonly CapVoiceMuteGate inputMute=new CapVoiceMuteGate(),outputMute=new CapVoiceMuteGate();
        private readonly Dictionary<VivoxParticipant,CapVoiceMuteGate> muteGates=new Dictionary<VivoxParticipant,CapVoiceMuteGate>();
        private float nextVolumeUpdate;
        private int outputLevel=int.MinValue;
        private Task deviceInitialization;
        private string deviceStatus="음성 장치 준비 중…";
        private const string SystemDevice="Default System Device";
        private const string CommunicationDevice="Default Communication Device";
        private string joined="", attempted="";
        private float retryAt;
        private readonly Dictionary<VivoxParticipant,float> levels=new Dictionary<VivoxParticipant,float>();
        private readonly Dictionary<VivoxParticipant,int> applied=new Dictionary<VivoxParticipant,int>();
        private IVivoxService Service => VivoxService.Instance;
        private string Desired => VoiceEnabled && CapRelaySession.Instance!=null && CapRelaySession.Instance.Connected ? CapRelaySession.Instance.VoiceRoom : "";
        public string InputName => initialized ? DeviceLabel(Service.ActiveInputDevice?.DeviceID,Service.ActiveInputDevice?.DeviceName,Service.EffectiveInputDevice?.DeviceName) : deviceStatus;
        public string OutputName => initialized ? DeviceLabel(Service.ActiveOutputDevice?.DeviceID,Service.ActiveOutputDevice?.DeviceName,Service.EffectiveOutputDevice?.DeviceName) : deviceStatus;
        private static string DeviceLabel(string id,string name,string effective=null)
        {
            string label=id==SystemDevice?"시스템 기본 장치 (자동)":id==CommunicationDevice?"시스템 기본 통신 장치":name??"장치 없음";
            if((id==SystemDevice || id==CommunicationDevice) && !string.IsNullOrEmpty(effective) && effective!=id)label+=" · "+effective;
            return label;
        }
        public async void PrepareDevices()
        {
            try{await EnsureDevicesAsync();}
            catch(Exception error){deviceStatus="장치 준비 실패 · 다시 연결을 눌러 주세요";Status=deviceStatus;Debug.LogWarning("[CAP-VOICE] Device setup: "+error.GetType().Name);}
        }
        private Task EnsureDevicesAsync()
        {
            if(deviceInitialization!=null && !deviceInitialization.IsCompleted)return deviceInitialization;
            if(initialized)return Task.CompletedTask;
            if(deviceInitialization==null || deviceInitialization.IsCompleted)deviceInitialization=InitializeDevicesAsync();
            return deviceInitialization;
        }
        private async Task InitializeDevicesAsync()
        {
            deviceStatus="음성 장치 준비 중…";
            await CapRelaySession.Instance.EnsureServicesAsync();
            if(disposed)return;
            await Service.InitializeAsync();
            if(disposed){Service.Uninitialize();return;}
            initialized=true;
            Service.ParticipantAddedToChannel+=OnParticipantAdded;
            Service.ParticipantRemovedFromChannel+=OnParticipantRemoved;
            Service.LoggedOut+=OnLoggedOut;
            Service.ChannelLeft+=OnChannelLeft;
            Service.ConnectionRecovering+=OnRecovering;
            Service.ConnectionRecovered+=OnRecovered;
            Service.ConnectionFailedToRecover+=OnRecoveryFailed;
            ResetDeviceCommands();Silence();
            await RestoreDevicesAsync();
        }

        private void Awake()
        {
            Instance=this;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            verification=Array.IndexOf(Environment.GetCommandLineArgs(),"--loading-ready-verify")>=0 || Array.IndexOf(Environment.GetCommandLineArgs(),"--voice-verify")>=0 || Array.IndexOf(Environment.GetCommandLineArgs(),"--meeting-verify")>=0;
#endif
            VoiceEnabled=PlayerPrefs.GetInt("Cap.Voice.Enabled",1)!=0;
            PushToTalk=PlayerPrefs.GetInt("Cap.Voice.PTT",1)!=0;
            Muted=PlayerPrefs.GetInt("Cap.Voice.Muted",0)!=0;
            VoiceVolume=Mathf.Clamp(PlayerPrefs.GetFloat("Cap.Voice.Volume",1),0,CapVoiceRange.MaximumVoiceVolume);
        }
        public void SetEnabled(bool value){VoiceEnabled=value;Save();Retry();if(!value)Silence();}
        public void SetPushToTalk(bool value){PushToTalk=value;Save();ApplyMicrophone();}
        public void SetMuted(bool value){Muted=value;Save();ApplyMicrophone();}
        public void SetVolume(float value){VoiceVolume=Mathf.Clamp(value,0,CapVoiceRange.MaximumVoiceVolume);Save();}
        private void Save()
        {
            PlayerPrefs.SetInt("Cap.Voice.Enabled",VoiceEnabled?1:0);PlayerPrefs.SetInt("Cap.Voice.PTT",PushToTalk?1:0);
            PlayerPrefs.SetInt("Cap.Voice.Muted",Muted?1:0);PlayerPrefs.SetFloat("Cap.Voice.Volume",VoiceVolume);PlayerPrefs.Save();
        }
        public void Retry(){attempted="";retryAt=0;reconnectRequested=true;if(!initialized)PrepareDevices();}
        public void StopDeviceTest()
        {
            if(deviceTest.Running)DeviceTestStatus="테스트가 종료되었습니다. 다시 시작할 수 있습니다.";
            deviceTest.Dispose(); MicrophoneLevel=0;
        }
        public void ToggleMicrophoneTest()
        {
            if(TestingMicrophone){StopDeviceTest();DeviceTestStatus="마이크 테스트를 중지했습니다.";return;}
            StartDeviceTest(true);
        }
        public void ToggleSpeakerTest()
        {
            if(TestingSpeaker){StopDeviceTest();DeviceTestStatus="스피커 테스트를 중지했습니다.";return;}
            StartDeviceTest(false);
        }
        private void StartDeviceTest(bool input)
        {
            if(!CanTestDevices || !CapOptions.IsOpen)return;
            StopDeviceTest();
            if(!input && VoiceVolume*AudioListener.volume<=.001f)
            {DeviceTestStatus="전체 음량과 친구 목소리 음량을 높인 뒤 테스트하세요.";return;}
            try
            {
                testEndpoint=input?Service.EffectiveInputDevice?.DeviceID:Service.EffectiveOutputDevice?.DeviceID;
                if(input)deviceTest.StartInput(testEndpoint);
                else deviceTest.StartOutput(testEndpoint,VoiceVolume*AudioListener.volume);
                testDeadline=Time.unscaledTime+(input?30:3);lastInputBufferAt=Time.unscaledTime;
                DeviceTestStatus=input?"마이크에 말해 보세요. 30초 뒤 자동으로 종료됩니다.":"선택한 스피커로 테스트 소리를 재생합니다.";
            }
            catch(Exception error)
            {
                StopDeviceTest();
                DeviceTestStatus=input?"마이크를 열 수 없습니다. 장치 연결과 Windows 마이크 접근 권한을 확인하세요.":"선택한 출력 장치를 열 수 없습니다. 장치를 다시 선택하세요.";
                Debug.LogWarning("[CAP-AUDIO-TEST] "+error.Message);
            }
        }
        private void UpdateDeviceTest()
        {
            if(!deviceTest.Running)return;
            string endpoint=deviceTest.IsInput?Service.EffectiveInputDevice?.DeviceID:Service.EffectiveOutputDevice?.DeviceID;
            if(!CapOptions.IsOpen || !CanTestDevices || endpoint!=testEndpoint || Time.unscaledTime>=testDeadline)
            {StopDeviceTest();DeviceTestStatus="테스트가 종료되었습니다. 다시 시작할 수 있습니다.";return;}
            try
            {
                bool input=deviceTest.IsInput;int count=deviceTest.BuffersRead;
                deviceTest.Poll();
                if(deviceTest.BuffersRead!=count)lastInputBufferAt=Time.unscaledTime;
                if(input && Time.unscaledTime-lastInputBufferAt>2)
                {StopDeviceTest();DeviceTestStatus="마이크 입력 응답이 없습니다. 연결과 접근 권한을 확인하세요.";return;}
                MicrophoneLevel=Mathf.MoveTowards(MicrophoneLevel,deviceTest.Level,Time.unscaledDeltaTime*3);
                if(!deviceTest.Running)DeviceTestStatus="재생 완료 · 소리가 들렸는지 직접 확인해 주세요.";
            }
            catch(Exception){StopDeviceTest();DeviceTestStatus="장치 테스트가 중단되었습니다. 장치를 다시 선택하세요.";}
        }
        private void Update()
        {
            UpdateDeviceTest();
            if(Application.isFocused && CapControls.Pressed(CapAction.Mute))SetMuted(!Muted);
            string desired=Desired;
            if(desired!=joined)Silence();
            if(!working && !changingDevice && (!recovering || desired!=joined || reconnectRequested) && (resetRequired || reconnectRequested || desired!=joined || (desired.Length>0 && !Ready) || (desired.Length==0 && initialized && Service.IsLoggedIn)) && (desired!=attempted || Time.unscaledTime>=retryAt))
                _=ReconcileAsync(desired);
            ApplyMicrophone();
        }
        private async Task ReconcileAsync(string desired)
        {
            StopDeviceTest();
            working=true;reconnectRequested=false;attempted=desired;retryAt=Time.unscaledTime+30;
            try
            {
                Ready=false;
                if(resetRequired && initialized)
                {
                    // Cancel stale native requests before creating another login/channel.
                    Unsubscribe();Service.Uninitialize();initialized=false;resetRequired=false;
                }
                if(initialized && Service.IsLoggedIn)
                {Silence();await Service.LeaveAllChannelsAsync();await Service.LogoutAsync();}
                joined="";recovering=false;levels.Clear();applied.Clear();muteGates.Clear();ResetDeviceCommands();
                if(disposed || desired.Length==0 || Desired!=desired){Status="";return;}
                Status="음성 서버 연결 중…";
                await EnsureDevicesAsync();
                SetDeviceMute(true,true);SetDeviceMute(false,true);
                if(disposed || Desired!=desired)return;
                await Service.LoginAsync(new LoginOptions { DisplayName="탐정",EnableTTS=false });
                ResetDeviceCommands();SetDeviceMute(true,true);
                if(disposed || Desired!=desired){await Service.LogoutAsync();return;}
                await Service.JoinGroupChannelAsync(desired,ChatCapability.AudioOnly,new ChannelOptions { MakeActiveChannelUponJoining=true });
                if(disposed || Desired!=desired){await Service.LeaveAllChannelsAsync();await Service.LogoutAsync();return;}
                joined=desired;Ready=true;Status="음성 연결됨";
                // Unknown/unmapped participants stay muted. Apply range before enabling playback.
                foreach(var channel in Service.ActiveChannels.Values)foreach(var participant in channel)
                    if(!participant.IsSelf && !muteGates.ContainsKey(participant))OnParticipantAdded(participant);
                Mix();
                Debug.Log("[CAP-VOICE] Room audio connected.");
            }
            catch(Exception error)
            {
                Ready=false;Silence();joined="";resetRequired=initialized;retryAt=Time.unscaledTime+10;
                Status="음성 연결 실패 · 서비스 활성화 / 네트워크를 확인하고 재연결하세요.";
                Debug.LogWarning("[CAP-VOICE] "+error.GetType().Name+": "+error.Message);
            }
            finally{working=false;}
        }
        private void ResetDeviceCommands(){inputMute.Reset();outputMute.Reset();outputLevel=int.MinValue;}
        private void OnLoggedOut(){Ready=false;Transmitting=false;levels.Clear();applied.Clear();muteGates.Clear();}
        private void OnChannelLeft(string channel){if(channel==joined && !working)FailAudio("음성 채널 연결이 종료되었습니다.");}
        private void OnRecovering(){recovering=true;Ready=false;Silence();Status="음성 네트워크 복구 중…";}
        private void OnRecovered()
        {
            recovering=false;ResetDeviceCommands();applied.Clear();
            foreach(var gate in muteGates.Values)gate.Reset();
            Ready=!working && !resetRequired && joined.Length>0 && Desired==joined && Service.ActiveChannels.ContainsKey(joined);
            Status=Ready?"음성 연결됨":"음성 재연결 대기 중…";
        }
        private void OnRecoveryFailed(){recovering=false;FailAudio("음성 네트워크를 복구하지 못했습니다.");}
        private void FailAudio(string reason)
        {
            if(resetRequired || disposed)return;
            Ready=false;Transmitting=false;recovering=false;resetRequired=true;retryAt=Time.unscaledTime+10;
            Status="음성 응답 지연 · 잠시 후 다시 연결합니다. 옵션에서 수동 재연결도 가능합니다.";
            Debug.LogWarning("[CAP-VOICE] "+reason+" Queued audio requests stopped; reconnect scheduled.");
            Silence();
        }
        private void SetDeviceMute(bool input,bool muted)
        {
            if(!initialized)return;
            var gate=input?inputMute:outputMute;
            bool observed=input?Service.IsInputDeviceMuted:Service.IsOutputDeviceMuted;
            if(gate.TryRequest(muted,observed,Time.unscaledTime,out bool timeout))
            {
                try
                {
                    if(input){if(muted)Service.MuteInputDevice();else Service.UnmuteInputDevice();}
                    else {if(muted)Service.MuteOutputDevice();else Service.UnmuteOutputDevice();}
                }
                catch(Exception){FailAudio("음성 장치 요청 실패");}
            }
            if(timeout)FailAudio("음성 장치 응답 시간 초과");
        }
        private void SetParticipantMute(VivoxParticipant participant,bool muted)
        {
            if(!muteGates.TryGetValue(participant,out var gate))return;
            if(gate.TryRequest(muted,participant.IsMuted,Time.unscaledTime,out bool timeout))
            {
                try{if(muted)participant.MutePlayerLocally();else participant.UnmutePlayerLocally();}
                catch(Exception){FailAudio("참가자 음성 요청 실패");}
            }
            if(timeout)FailAudio("참가자 음소거 응답 시간 초과");
        }
        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if(participant.IsSelf)return;
            if(muteGates.ContainsKey(participant))return;
            muteGates[participant]=new CapVoiceMuteGate();levels[participant]=0;applied[participant]=-999;
            SetParticipantMute(participant,true);
        }
        private void OnParticipantRemoved(VivoxParticipant participant){levels.Remove(participant);applied.Remove(participant);muteGates.Remove(participant);}
        private void ApplyMicrophone()
        {
            if(!initialized)return;
            bool transmit=!verification && Ready && Desired==joined && VoiceEnabled && !Muted && Application.isFocused && !CapControls.Blocked && (!PushToTalk || CapControls.Held(CapAction.PushToTalk));
            SetDeviceMute(true,!transmit);
            Transmitting=transmit && !Service.IsInputDeviceMuted;
        }
        private void Silence()
        {
            Transmitting=false;
            if(!initialized)return;
            SetDeviceMute(true,true);SetDeviceMute(false,true);
        }
        private void LateUpdate(){if(Ready && Desired==joined)Mix();}
        public float GainFor(string playerId)
        {
            foreach(var pair in levels)if(pair.Key.PlayerId==playerId)return pair.Value;
            return -1;
        }
        private void Mix()
        {
            var local=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<CapNetworkPlayer>();
            var camera=Camera.main;
            if(local==null || camera==null){SetDeviceMute(false,true);return;}
            if(AuthenticationService.Instance.IsSignedIn && local.VoicePlayerId.Value.Length==0)
                local.VoicePlayerId.Value=new Unity.Collections.FixedString64Bytes(AuthenticationService.Instance.PlayerId);
            var players=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None);
            float master=VoiceVolume*AudioListener.volume;
            SetDeviceMute(false,master<=.001f);
            bool updateVolume=Time.unscaledTime>=nextVolumeUpdate;
            if(updateVolume)
            {
                nextVolumeUpdate=Time.unscaledTime+.1f;
                int level=CapVoiceRange.Volume(master);
                if(level!=outputLevel){outputLevel=level;Service.SetOutputDeviceVolume(level);}
            }
            foreach(var channel in Service.ActiveChannels.Values)
            foreach(var participant in channel)
            {
                if(participant.IsSelf)continue;
                CapNetworkPlayer remote=null;
                foreach(var candidate in players)
                    if(candidate.IsSpawned && candidate.VoicePlayerId.Value.ToString()==participant.PlayerId){remote=candidate;break;}
                float gain=0;
                if(remote!=null)
                {
                    float radius=CapVoiceRange.FadeRadius(camera.orthographicSize,camera.aspect);
                    gain=CapVoiceRange.Gain(camera.WorldToViewportPoint(remote.VisualCenter),Vector2.Distance(local.VisualCenter,remote.VisualCenter),CapVoiceRange.FullVolumeRadius,radius,local.SameSpace(remote),local.InTown.Value && !local.InMeetingRoom.Value);
                }
                if(!levels.TryGetValue(participant,out float prior))prior=0;
                // Offscreen must be silent immediately. Smooth only audible changes.
                float next=gain<=0?0:Mathf.MoveTowards(prior,gain,Time.unscaledDeltaTime*4);
                levels[participant]=next;
                if(!Ready)return;
                if(next<=.001f){SetParticipantMute(participant,true);continue;}
                int level=CapVoiceRange.Volume(next);
                if(updateVolume && (!applied.TryGetValue(participant,out int previous) || level!=previous)){participant.SetLocalVolume(level);applied[participant]=level;}
                SetParticipantMute(participant,false);
            }
        }
        public sealed class DeviceOption
        {
            public string Id, Name;
            public bool Selected;
        }
        private bool changingDevice;
        public bool CanSelectDevices => initialized && !changingDevice;
        public List<DeviceOption> GetDevices(bool input)
        {
            var result=new List<DeviceOption>();
            if(!initialized)return result;
            if(input)
                foreach(var device in Service.AvailableInputDevices)
                    result.Add(new DeviceOption { Id=device.DeviceID,Name=DeviceLabel(device.DeviceID,device.DeviceName),Selected=device.DeviceID==Service.ActiveInputDevice?.DeviceID });
            else
                foreach(var device in Service.AvailableOutputDevices)
                    result.Add(new DeviceOption { Id=device.DeviceID,Name=DeviceLabel(device.DeviceID,device.DeviceName),Selected=device.DeviceID==Service.ActiveOutputDevice?.DeviceID });
            result.Sort((a,b)=>a.Id==b.Id?0:a.Id==SystemDevice?-1:b.Id==SystemDevice?1:string.Compare(a.Name,b.Name,StringComparison.CurrentCulture));
            return result;
        }
        public async void SelectDevice(bool input,string deviceId)
        {
            if(!CanSelectDevices)return;
            StopDeviceTest();
            changingDevice=true;
            try
            {
                if(input)
                {
                    foreach(var device in Service.AvailableInputDevices)
                        if(device.DeviceID==deviceId)
                        {
                            await Service.SetActiveInputDeviceAsync(device);
                            PlayerPrefs.SetString("Cap.Voice.Input",deviceId);PlayerPrefs.Save();return;
                        }
                }
                else
                {
                    foreach(var device in Service.AvailableOutputDevices)
                        if(device.DeviceID==deviceId)
                        {
                            await Service.SetActiveOutputDeviceAsync(device);
                            PlayerPrefs.SetString("Cap.Voice.Output",deviceId);PlayerPrefs.Save();return;
                        }
                }
                Status="선택한 장치가 분리되었습니다. 목록에서 다시 선택하세요.";
            }
            catch(Exception){Status="음성 장치 변경 실패 · Windows 소리 설정을 확인하세요.";}
            finally{changingDevice=false;}
        }
        private async Task RestoreDevicesAsync()
        {
            try
            {
                string input=PlayerPrefs.GetString("Cap.Voice.Input",SystemDevice);
                bool found=false;
                foreach(var device in Service.AvailableInputDevices)if(device.DeviceID==input){await Service.SetActiveInputDeviceAsync(device);found=true;break;}
                if(!found)foreach(var device in Service.AvailableInputDevices)if(device.DeviceID==SystemDevice){await Service.SetActiveInputDeviceAsync(device);break;}
                string output=PlayerPrefs.GetString("Cap.Voice.Output",SystemDevice);
                found=false;
                foreach(var device in Service.AvailableOutputDevices)if(device.DeviceID==output){await Service.SetActiveOutputDeviceAsync(device);found=true;break;}
                if(!found)foreach(var device in Service.AvailableOutputDevices)if(device.DeviceID==SystemDevice){await Service.SetActiveOutputDeviceAsync(device);break;}
            }
            catch(Exception){Debug.LogWarning("[CAP-VOICE] Saved audio device unavailable; using system default.");}
        }
        private void OnApplicationFocus(bool focused){if(!focused){StopDeviceTest();if(initialized){SetDeviceMute(true,true);Transmitting=false;}}}
        private void OnApplicationQuit(){StopDeviceTest();Silence();}
        private async void OnDestroy()
        {
            disposed=true;StopDeviceTest();Silence();
            if(Instance==this)Instance=null;
            if(!initialized)return;
            Unsubscribe();
            if(!working)try{if(Service.IsLoggedIn)await Service.LogoutAsync();}catch(Exception){ }
        }
        private void Unsubscribe()
        {
            Service.ParticipantAddedToChannel-=OnParticipantAdded;Service.ParticipantRemovedFromChannel-=OnParticipantRemoved;Service.LoggedOut-=OnLoggedOut;
            Service.ChannelLeft-=OnChannelLeft;Service.ConnectionRecovering-=OnRecovering;Service.ConnectionRecovered-=OnRecovered;Service.ConnectionFailedToRecover-=OnRecoveryFailed;
        }
    }
}

