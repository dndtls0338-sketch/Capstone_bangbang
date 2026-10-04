using System.Text;
using UnityEngine;

namespace Cap.Multiplayer
{
    public static class CapPlayerProfile
    {
        public static readonly string[] ColorNames={"주황","파랑","초록","분홍","보라","노랑","청록","흰색"};
        public static readonly Color[] Colors={new Color(1,.65f,.28f),new Color(.3f,.7f,1),new Color(.45f,.9f,.6f),new Color(.95f,.45f,.7f),new Color(.7f,.5f,1),new Color(1,.9f,.3f),new Color(.2f,.9f,.85f),new Color(.92f,.94f,1)};
        public static string SavedName=>CleanName(PlayerPrefs.GetString("Cap.Profile.Name",""));
        public static int SavedColor=>PlayerPrefs.GetInt("Cap.Profile.Color",-1);
        public static string Status="";
        public static bool Verification
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--slot-verify")>=0;
#else
                return false;
#endif
            }
        }
        public static Color GetColor(int index)=>index>=0 && index<Colors.Length?Colors[index]:Color.gray;
        public static string CleanName(string value)
        {
            var result=new StringBuilder();
            foreach(char c in (value??""))
            {
                if(result.Length>=12)break;
                if(char.IsLetterOrDigit(c)||c=='_'||c=='-'||c==' ')result.Append(c);
            }
            return result.ToString().Trim();
        }
        public static void Save(string name,int color)
        {
            PlayerPrefs.SetString("Cap.Profile.Name",CleanName(name));
            PlayerPrefs.SetInt("Cap.Profile.Color",color);PlayerPrefs.Save();
        }
    }
}
