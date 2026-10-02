using System.Collections;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapWalkSmoke : MonoBehaviour
    {
        public static Vector2? InputOverride;
        private static string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--cap-walk-verify")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--cap-bike-verify")>=0)
                new GameObject("Walking verification").AddComponent<CapWalkSmoke>();
#endif
        }
        private IEnumerator Start()
        {
            output=Application.dataPath+"/../WalkVerification"; Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(3);
            var nm=NetworkManager.Singleton;
            bool bike=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--cap-bike-verify")>=0;
            if(nm==null || !nm.IsConnectedClient) { Debug.LogError("[WALK-VERIFY] No connection"); Application.Quit(2); yield break; }
            if(!nm.IsServer)
            {
                int previous=-1; bool? previousBike=null;
                for(int i=0;i<300;i++)
                {
                    foreach(var p in FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None))
                    {
                        if(p.OwnerClientId!=0 || (previous==p.Locomotion.Value && previousBike==p.Riding.Value)) continue;
                        previous=p.Locomotion.Value;
                        previousBike=p.Riding.Value;
                        var anim=p.GetComponentInChildren<CapCharacterAnimation>();
                        Debug.Log($"[WALK-REMOTE] state={previous} direction={anim.CurrentDirection} frame={anim.CurrentFrame} town={p.InTown.Value} riding={p.Riding.Value}");
                    }
                    yield return new WaitForSecondsRealtime(.2f);
                }
                Application.Quit(); yield break;
            }
            int count=FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length;
            if(count!=1 || !CapWarmTown.CanStart) Debug.LogError($"[WALK-VERIFY] Solo start unavailable count={count}");
            else Debug.Log("[WALK-VERIFY] Solo start available: 1 player");
            CapWarmTown.StartForAll(true);
            yield return new WaitForSecondsRealtime(2);
            if(!CapWarmTown.Instance.InTown) Debug.LogError("[WALK-VERIFY] Solo start failed");
            else Debug.Log("[WALK-VERIFY] Solo start reached town");
            // Give the verification launcher time to attach three late clients.
            float joinDeadline=Time.realtimeSinceStartup+20;
            while(nm.ConnectedClientsIds.Count<4 && Time.realtimeSinceStartup<joinDeadline) yield return null;
            yield return new WaitForSecondsRealtime(2);
            var player=nm.LocalClient.PlayerObject.GetComponent<CapNetworkPlayer>();
            var art=player.GetComponentInChildren<CapCharacterAnimation>();
            var nt=player.GetComponent<NetworkTransform>();
            if(bike)
            {
                InputOverride=Vector2.zero;
                nt.Teleport(new Vector3(96,-2,0),Quaternion.identity,player.transform.localScale);
                yield return new WaitForSecondsRealtime(.2f);
                InputOverride=Vector2.right;
                yield return new WaitForSecondsRealtime(.2f);
                var start=player.transform.position;
                yield return new WaitForSecondsRealtime(.5f);
                float walked=Vector3.Distance(start,player.transform.position);
                InputOverride=Vector2.zero;
                yield return new WaitForSecondsRealtime(.2f);
                nt.Teleport(new Vector3(96,-2,0),Quaternion.identity,player.transform.localScale);
                player.ToggleBicycle();
                yield return new WaitForSecondsRealtime(.4f);
                if(!player.Riding.Value || !art.ShowingBicycle) Debug.LogError("[BIKE-VERIFY] Mount failed");
                InputOverride=Vector2.right;
                yield return new WaitForSecondsRealtime(.2f);
                start=player.transform.position;
                float until=Time.realtimeSinceStartup+.5f;
                float worstFrame=0; int samples=0,rootHolds=0,visualHolds=0;
                var rootPrevious=player.transform.position; var renderPrevious=player.RenderPosition;
                while(Time.realtimeSinceStartup<until)
                {
                    yield return new WaitForEndOfFrame();
                    worstFrame=Mathf.Max(worstFrame,Time.unscaledDeltaTime); samples++;
                    if(Vector3.Distance(rootPrevious,player.transform.position)<.0001f) rootHolds++;
                    if(Vector3.Distance(renderPrevious,player.RenderPosition)<.0001f) visualHolds++;
                    rootPrevious=player.transform.position; renderPrevious=player.RenderPosition;
                }
                float ratio=Vector3.Distance(start,player.transform.position)/walked;
                if(ratio<1.55f || ratio>2.1f) Debug.LogError("[BIKE-VERIFY] Speed ratio unexpected="+ratio);
                else Debug.Log($"[BIKE-VERIFY] Speed ratio={ratio:F2}; render samples={samples}, physics holds={rootHolds}, visual holds={visualHolds}, worst frame ms={worstFrame*1000:F1}");
                InputOverride=Vector2.zero;
            }
            for(int direction=0;direction<8;direction++)
            {
                InputOverride=Vector2.zero;
                nt.Teleport(new Vector3(98,-2,0),Quaternion.identity,player.transform.localScale);
                yield return new WaitForSecondsRealtime(.35f);
                InputOverride=new Vector2(Mathf.Cos(direction*Mathf.PI/4),Mathf.Sin(direction*Mathf.PI/4));
                yield return new WaitForSecondsRealtime(.4f);
                if(player.Locomotion.Value!=(direction|8) || art.CurrentDirection!=direction || art.CurrentFrame==0)
                    Debug.LogError($"[WALK-VERIFY] Direction {direction} failed state={player.Locomotion.Value} pose={art.CurrentDirection}/{art.CurrentFrame}");
                else Debug.Log($"[WALK-VERIFY] Direction {direction} walks, frame={art.CurrentFrame}");
                var first=art.GetComponent<SpriteRenderer>().sprite;
                float frameDeadline=Time.realtimeSinceStartup+1;
                while(first==art.GetComponent<SpriteRenderer>().sprite && Time.realtimeSinceStartup<frameDeadline) yield return null;
                if(first==art.GetComponent<SpriteRenderer>().sprite) Debug.LogError("[WALK-VERIFY] Frame did not advance");
                else Debug.Log("[WALK-VERIFY] Frame advanced direction="+direction);
                Capture($"direction-{direction}.png");
                InputOverride=Vector2.zero;
                yield return new WaitForSecondsRealtime(.35f);
                if(player.Locomotion.Value!=direction || art.CurrentFrame!=0 || art.CurrentDirection!=direction)
                    Debug.LogError("[WALK-VERIFY] Idle failed direction="+direction);
                else Debug.Log("[WALK-VERIFY] Idle keeps direction="+direction);
            }
            // Walk straight into a wall: stop cycling once actual movement stops.
            nt.Teleport(new Vector3(65,-6.3f,0),Quaternion.identity,player.transform.localScale);
            InputOverride=Vector2.up;
            yield return new WaitForSecondsRealtime(.8f);
            if((player.Locomotion.Value&8)!=0 || art.CurrentFrame!=0) Debug.LogError("[WALK-VERIFY] Walk animation continues against wall");
            else Debug.Log("[WALK-VERIFY] Wall collision returns to idle");
            InputOverride=Vector2.zero;
            if(bike)
            {
                player.ToggleBicycle();
                yield return new WaitForSecondsRealtime(.4f);
                if(player.Riding.Value || art.ShowingBicycle) Debug.LogError("[BIKE-VERIFY] Dismount failed");
                else Debug.Log("[BIKE-VERIFY] Dismount restored walking sprites");
                player.ToggleBicycle();
                yield return new WaitForSecondsRealtime(.4f);
            }
            Debug.Log("[WALK-VERIFY] Players after late joins="+FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length);
            yield return new WaitForSecondsRealtime(3);
            CapWarmTown.StartForAll(false);
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[WALK-VERIFY] Returned lobby="+!CapWarmTown.Instance.InTown);
            if(bike && player.Riding.Value) Debug.LogError("[BIKE-VERIFY] Lobby did not reset bicycle");
            CapWarmTown.StartForAll(true);
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[WALK-VERIFY] Second start="+CapWarmTown.Instance.InTown);
            InputOverride=null;
            CapRelaySession.Instance.LeaveRoom();
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[WALK-VERIFY] Finished"); Application.Quit();
        }
        private static void Capture(string name)
        {
            var rt=RenderTexture.GetTemporary(1200,750,24,RenderTextureFormat.ARGB32);
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var old=RenderTexture.active; RenderTexture.active=rt;
            var image=new Texture2D(1200,750,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1200,750),0,0); image.Apply();
            File.WriteAllBytes(output+"/"+name,image.EncodeToPNG());
            Destroy(image); RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt);
        }
        private void OnDestroy() { InputOverride=null; }
    }
}
