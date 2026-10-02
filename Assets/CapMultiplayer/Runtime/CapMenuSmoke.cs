using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEngine;
namespace Cap.Multiplayer {
 public sealed class CapMenuSmoke:MonoBehaviour {
  private bool failed;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  private static void Boot(){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
   if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--menu-verify")>=0)new GameObject("Menu verification").AddComponent<CapMenuSmoke>();
#endif
  }
  private void Check(bool condition,string label){if(!condition){failed=true;Debug.LogError("[MENU-VERIFY] FAIL "+label);}else Debug.Log("[MENU-VERIFY] PASS "+label);}
  private IEnumerator Start(){
   yield return new WaitForSecondsRealtime(2);
   var ui=FindFirstObjectByType<CapLobbyUI>();var session=CapRelaySession.Instance;
   Check(ui.CurrentScreen=="Home","starts on four-button home screen");
   ui.OpenJoin();yield return null;Check(ui.CurrentScreen=="Join","join page opens");
   ui.JoinFromMenu(" ");Check(!session.Busy && ui.Notice.Length>0,"empty join code rejected without network request");
   ui.BackToHome();Check(ui.CurrentScreen=="Home","join back button");
   bool hadVolume=PlayerPrefs.HasKey("Cap.MasterVolume");float previous=PlayerPrefs.GetFloat("Cap.MasterVolume",1);
   ui.OpenOptions();Check(ui.CurrentScreen=="Options","options page opens");
   ui.SetVolume(.37f);ui.SaveOptions();Check(Mathf.Abs(AudioListener.volume-.37f)<.001f && Mathf.Abs(PlayerPrefs.GetFloat("Cap.MasterVolume")-.37f)<.001f,"volume applies and persists");
   ui.SetVolume(previous);ui.BackToHome();if(!hadVolume)PlayerPrefs.DeleteKey("Cap.MasterVolume");PlayerPrefs.Save();
   session.StartLocal(true);yield return new WaitForSecondsRealtime(2);
   Check(session.Connected && ui.CurrentScreen=="Lobby","connection opens lobby automatically");
   CapWarmTown.StartForAll(true);yield return new WaitForSecondsRealtime(1);
   Check(CapWarmTown.Instance.InTown && !ui.enabled,"start game hides menu");
   CapWarmTown.StartForAll(false);yield return new WaitForSecondsRealtime(1);
   Check(ui.enabled && ui.CurrentScreen=="Lobby","town return restores lobby");
   session.LeaveRoom();yield return new WaitForSecondsRealtime(2);
   Check(!session.Connected && ui.CurrentScreen=="Home","leave room returns to home");
   string path=Application.dataPath+"/../menu-result.txt";File.WriteAllText(path,failed?"FAILED":"PASSED");
   Debug.Log("[MENU-VERIFY] "+(failed?"FAILED":"PASSED")+"; invoking real quit button action");
   ui.ExitGame();
  }
 }
}
