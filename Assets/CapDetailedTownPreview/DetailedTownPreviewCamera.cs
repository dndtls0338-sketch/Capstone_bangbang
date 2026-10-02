using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace Cap.DetailedPreview {
 public sealed class DetailedTownPreviewCamera : MonoBehaviour {
  public Transform player;
  public SpriteRenderer playerRenderer;
  public Camera view;
  bool overview;
  public void Focus(float x,float y) {
   overview=false;
   player.gameObject.SetActive(true);
   player.position=new Vector3(x/100f,-y/100f,0);
   Apply();
  }
  public void Apply() {
   float targetAspect=1534f/1025f,actual=(float)Screen.width/Mathf.Max(1,Screen.height);
   if(actual>targetAspect){float w=targetAspect/actual;view.rect=new Rect((1-w)/2,0,w,1);}
   else{float h=actual/targetAspect;view.rect=new Rect(0,(1-h)/2,1,h);}
   view.orthographicSize=overview?17.92f:5.125f;
   view.transform.position=overview?new Vector3(26.88f,-17.92f,-10):new Vector3(player.position.x,player.position.y+.45f,-10);
   playerRenderer.sortingOrder=1000+Mathf.RoundToInt(-player.position.y*100);
  }
  void LateUpdate() {
#if ENABLE_INPUT_SYSTEM
   var k=Keyboard.current;
   if(k!=null&&!overview){Vector2 move=Vector2.zero;if(k.wKey.isPressed||k.upArrowKey.isPressed)move.y++;if(k.sKey.isPressed||k.downArrowKey.isPressed)move.y--;if(k.aKey.isPressed||k.leftArrowKey.isPressed)move.x--;if(k.dKey.isPressed||k.rightArrowKey.isPressed)move.x++;player.position+=(Vector3)(move.normalized*3.5f*Time.unscaledDeltaTime);player.position=new Vector3(Mathf.Clamp(player.position.x,.5f,53.26f),Mathf.Clamp(player.position.y,-35.34f,-.5f),0);}
#endif
   Apply();
  }
  void OnGUI(){
   GUILayout.BeginArea(new Rect(12,12,650,82),GUI.skin.box);
   GUILayout.Label("ASSET SCALE PREVIEW | WASD / arrows | terrain = dimension blockout");
   GUILayout.BeginHorizontal();
   if(GUILayout.Button("Shops"))Focus(1365,2485);
   if(GUILayout.Button("Campus"))Focus(3970,1270);
   if(GUILayout.Button("Hospital"))Focus(1890,880);
   if(GUILayout.Button("Park"))Focus(4050,3120);
   if(GUILayout.Button("Overview")){overview=true;player.gameObject.SetActive(false);}
   GUILayout.EndHorizontal();GUILayout.EndArea();
  }
 }
}

