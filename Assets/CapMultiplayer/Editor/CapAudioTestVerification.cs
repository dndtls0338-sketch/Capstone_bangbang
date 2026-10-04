using System;
using Cap.Multiplayer;
using UnityEngine;

namespace Cap.Editor
{
    public static class CapAudioTestVerification
    {
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Debug.Log("[AUDIO-MATH] PASS "+message);}
        public static void Build()
        {
            Check(CapAudioDeviceTest.Measure(new short[100],100)==0,"silence has an empty meter");
            var quiet=new short[100];var loud=new short[100];
            for(int i=0;i<100;i++){quiet[i]=1000;loud[i]=16000;}
            Check(CapAudioDeviceTest.Measure(loud,100)>CapAudioDeviceTest.Measure(quiet,100),"louder PCM gives a larger meter");
            var tone=CapAudioDeviceTest.CreateTone(1);int peak=0;
            foreach(var sample in tone)peak=Math.Max(peak,Math.Abs((int)sample));
            Check(peak>4000 && peak<5000 && tone[0]==0 && tone[tone.Length-1]==0,"test tone is bounded and fades to silence");
            foreach(var sample in CapAudioDeviceTest.CreateTone(0))if(sample!=0)throw new Exception("Zero gain must be silent");
            CapWarmTownSetup.Build();
        }
    }
}
