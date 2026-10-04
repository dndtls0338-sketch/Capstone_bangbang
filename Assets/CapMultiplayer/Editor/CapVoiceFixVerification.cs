using System;
using Cap.Multiplayer;
using UnityEngine;
namespace Cap.Editor
{
    public static class CapVoiceFixVerification
    {
        private static void Check(bool result,string name){if(!result)throw new Exception(name);Debug.Log("[VOICE-GATE] PASS "+name);}
        public static void Build()
        {
            var gate=new CapVoiceMuteGate();int requests=0,timeouts=0;
            for(int i=0;i<1200;i++)
            {
                if(gate.TryRequest(true,false,i/120f,out bool timeout))requests++;
                if(timeout)timeouts++;
            }
            Check(requests==1 && timeouts==1,"10-second missing acknowledgement sends ONE request and ONE failure");
            gate.Reset();
            Check(gate.TryRequest(true,false,0,out _),"initial mute requested");
            Check(!gate.TryRequest(false,false,.1f,out _),"reversal waits for pending acknowledgement");
            Check(gate.TryRequest(false,true,.2f,out _),"latest intent sent after old acknowledgement");
            Check(!gate.TryRequest(false,false,.3f,out _),"stable acknowledged state produces no duplicate");
            gate.Reset();Check(gate.TryRequest(true,false,0,out _),"new connection resets failed command state");
            CapWarmTownSetup.Build();
        }
    }
}
