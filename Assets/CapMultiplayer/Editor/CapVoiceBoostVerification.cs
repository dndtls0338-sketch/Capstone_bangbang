using System;
using Cap.Multiplayer;
using UnityEngine;
namespace Cap.Editor
{
    public static class CapVoiceBoostVerification
    {
        private static void Check(bool value,string label){if(!value)throw new Exception(label);Debug.Log("[VOICE-BOOST] PASS "+label);}
        public static void Build()
        {
            Check(CapVoiceRange.Volume(1)==0 && CapVoiceRange.Volume(2)==6,"unity volume and capped 200 percent gain");
            Check(CapVoiceRange.Volume(.5f)==-6 && CapVoiceRange.Volume(9)==6,"attenuation preserved and excessive gain capped");
            float oldRadius=10.25f*Mathf.Sqrt(1+(16f/9)*(16f/9));
            float radius=CapVoiceRange.FadeRadius(10.25f,16f/9);
            Check(Mathf.Abs(radius-oldRadius*.5f)<.001f,"hearing radius halved");
            var view=new Vector3(.6f,.5f,1);
            for(int i=0;i<=20;i++)
            {
                float distance=oldRadius*i/20;
                Check(Mathf.Abs(CapVoiceRange.Gain(view,distance,2.5f,oldRadius,true,true)-CapVoiceRange.Gain(view,distance*.5f,CapVoiceRange.FullVolumeRadius,radius,true,true))<.0001f,"falloff curve compressed to half distance "+i);
            }
            Check(CapVoiceRange.Gain(view,radius+.01f,CapVoiceRange.FullVolumeRadius,radius,true,true)==0,"inside screen but outside range silent");
            Check(CapVoiceRange.Gain(view,100,CapVoiceRange.FullVolumeRadius,radius,true,false)==1,"lobby global voice preserved");
            var quiet=CapAudioDeviceTest.CreateTone(1);var loud=CapAudioDeviceTest.CreateTone(2);
            for(int i=0;i<quiet.Length;i++)if(Math.Abs(loud[i]-2*quiet[i])>1)throw new Exception("Speaker test must reflect 200 percent");
            CapWarmTownSetup.Build();
        }
    }
}
