using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Cap.Multiplayer
{
    public sealed class CapTownSmoke : MonoBehaviour
    {
        public static bool Running;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--cap-town-verify")>=0)
                new GameObject("Town automated verification").AddComponent<CapTownSmoke>();
#endif
        }
        private IEnumerator Start()
        {
            Running=true;
            string output=Application.dataPath+"/../TownVerification";
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(8);
            var nm=NetworkManager.Singleton;
            if(nm==null || !nm.IsConnectedClient) { Debug.LogError("[TOWN-VERIFY] No connection"); Application.Quit(2); yield break; }
            var world=CapWarmTown.Instance;
            Debug.Log($"[TOWN-VERIFY] Connected local={nm.LocalClientId}, players={FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length}");
            if(nm.IsServer) CapWarmTown.StartForAll(true);
            float deadline=Time.realtimeSinceStartup+15;
            while(!world.InTown && Time.realtimeSinceStartup<deadline) yield return null;
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[TOWN-VERIFY] Town phase="+world.InTown);
            if(!world.InTown) Debug.LogError("[TOWN-VERIFY] Phase did not synchronize");
            if(nm.IsServer)
            {
                Validate(world);
                Capture(output+"/01-campus.png");
                yield return new WaitForSecondsRealtime(2);
                var obj=nm.LocalClient.PlayerObject;
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(26,42),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                Capture(output+"/02-shops.png");
                yield return new WaitForSecondsRealtime(2);
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(42,28),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                Capture(output+"/03-park.png");
                yield return new WaitForSecondsRealtime(2);
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(27,57.4f),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                Capture(output+"/07-shop-alley.png");
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(14,16),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                Capture(output+"/08-home-alley.png");
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(18.5f,33.8f),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                int behind=VisiblePlayerPixels(obj);Capture(output+"/09-behind-building.png");
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(18.5f,42.1f),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                int front=VisiblePlayerPixels(obj);Capture(output+"/10-front-building.png");
                if(behind>front*.15f || front<100)Debug.LogError($"[TOWN-VERIFY] Occlusion failed behind={behind} front={front}");
                else Debug.Log($"[TOWN-VERIFY] Alpha occlusion passed behind={behind} front={front}");
                var shop=world.ModularObjects["Convenience store"];
                shop.enabled=false;yield return null;Capture(output+"/11-building-removed-real-ground.png");shop.enabled=true;
                obj.GetComponent<NetworkTransform>().Teleport(CapWarmTown.PlanWorld(69,15.8f),Quaternion.identity,obj.transform.localScale);
                yield return new WaitForSecondsRealtime(2);
                var bike=world.ModularObjects["Campus bicycle"];
                int withBike=VisiblePlayerPixels(obj);bike.enabled=false;int withoutBike=VisiblePlayerPixels(obj);bike.enabled=true;
                if(withoutBike<100||withBike<withoutBike*.65f)Debug.LogError($"[TOWN-VERIFY] Bicycle alpha regression visible={withBike} baseline={withoutBike}");
                else Debug.Log($"[TOWN-VERIFY] Bicycle transparent-space regression passed visible={withBike} baseline={withoutBike}");
                Capture(output+"/12-bicycle-alpha.png");
                world.SetOverviewForVerification(true);
                yield return new WaitForSecondsRealtime(2);
                Capture(output+"/04-overview.png");
                world.SetOverviewForVerification(false);
                yield return new WaitForSecondsRealtime(2);
                CapWarmTown.StartForAll(false);
                yield return new WaitForSecondsRealtime(2);
                Debug.Log("[TOWN-VERIFY] Returned lobby="+!world.InTown);
                CapWarmTown.StartForAll(true);
                yield return new WaitForSecondsRealtime(2);
                Debug.Log("[TOWN-VERIFY] Second town entry="+world.InTown);
                yield return new WaitForSecondsRealtime(3);
                CapRelaySession.Instance.LeaveRoom();
            }
            else
            {
                for(int i=0;i<22;i++)
                {
                    yield return new WaitForSecondsRealtime(2);
                    Debug.Log($"[TOWN-VERIFY] Client phase={world.InTown}; players={FindObjectsByType<CapNetworkPlayer>(FindObjectsSortMode.None).Length}");
                }
            }
            yield return new WaitForSecondsRealtime(2);
            Debug.Log("[TOWN-VERIFY] Finished");
            Application.Quit();
        }
        private static Vector2 LP(Vector2 p) => new Vector2(p.x-44,32-p.y);
        private static void Validate(CapWarmTown world)
        {
            var spawn=CapWarmTown.Spawn(0);
            var start=Vector2Int.RoundToInt(new Vector2(spawn.x-CapWarmTown.OffsetX,spawn.y)*2);
            var seen=new HashSet<Vector2Int>{start};var queue=new Queue<Vector2Int>();queue.Enqueue(start);
            var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
            while(queue.Count>0){var p=queue.Dequeue();foreach(var d in dirs){var n=p+d;if(!seen.Contains(n)&&!world.Blocked((Vector2)n*.5f)){seen.Add(n);queue.Enqueue(n);}}}
            foreach(var p in new[]{new Vector2(73,14),new Vector2(73,20),new Vector2(58.5f,20),new Vector2(58.5f,42),new Vector2(26,42),new Vector2(42,28),new Vector2(14,16),new Vector2(41,16),new Vector2(27,57.5f),new Vector2(12,57.5f),new Vector2(46,57.5f),new Vector2(84,60)})
            {if(!seen.Contains(Vector2Int.RoundToInt(LP(p)*2)))Debug.LogError("[TOWN-VERIFY] Unreachable "+p);else Debug.Log("[TOWN-VERIFY] Reachable "+p);}
            for(int i=0;i<4;i++){var p=CapWarmTown.Spawn(i);if(world.Blocked(new Vector2(p.x-CapWarmTown.OffsetX,p.y)))Debug.LogError("[TOWN-VERIFY] Spawn blocked "+i);}
            int hits=0;
            for(float x=2;x<86;x+=.25f)if(world.Blocked(LP(new Vector2(x,46)),.6f))hits++;
            for(float x=3;x<55;x+=.25f)if(world.Blocked(LP(new Vector2(x,57.5f)),.35f))hits++;
            if(hits>0)Debug.LogError("[TOWN-VERIFY] Road clearance hits="+hits);else Debug.Log("[TOWN-VERIFY] Road clearance passed");
            if(world.ModularObjects.Count<60||world.GroundSurfaceCount<1)Debug.LogError("[TOWN-VERIFY] Missing independent assets or continuous ground");
            foreach(var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if(sr.sprite!=null&&sr.sprite.texture.name=="neighborhood")Debug.LogError("[TOWN-VERIFY] Rejected overview texture used");
            Debug.Log("[TOWN-VERIFY] Modular objects="+world.ModularObjects.Count+"; reachable cells="+seen.Count);
        }
        private static void Capture(string path)
        {
            var image=RenderFrame();
            File.WriteAllBytes(path,image.EncodeToPNG()); Destroy(image);
            Debug.Log("[TOWN-VERIFY] Captured "+Path.GetFileName(path));
        }
        private static int VisiblePlayerPixels(NetworkObject player)
        {
            var sr=player.transform.Find("Player art").GetComponent<SpriteRenderer>();
            var before=RenderFrame(); sr.enabled=false;
            var after=RenderFrame(); sr.enabled=true;
            var a=before.GetPixels32(); var b=after.GetPixels32(); int count=0;
            for(int i=0;i<a.Length;i++) if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>12) count++;
            Destroy(before); Destroy(after); return count;
        }
        private static Texture2D RenderFrame()
        {
            var camera=Camera.main;
            var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32);
            var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=rt };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            var old=RenderTexture.active; RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt);
            return image;
        }
    }
}
