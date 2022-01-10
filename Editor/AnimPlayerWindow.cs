using UnityEngine;
using UnityEditor;

namespace Reallusion.Import
{
    public class AnimPlayerWindow : EditorWindow
    {
        public static bool isShown = false;
        private static float xpadding = 6f;
#if SCENEVIEW_OVERLAY_COMPATIBLE
        private static float ypadding = 32f;  //delta of 26 pixels in case this window is used instead of an overlay 
#else
        private static float ypadding = 6f;
#endif
        private static float width = 320f;
        private static float height = 26f;
        
        public static void OnSceneGUI(SceneView sceneView)
        {
            height = 72f;
            if (AnimPlayerIMGUI.foldOut) height += 84f;
            if (FacialMorphIMGUI.foldOut) height += 80f;
            
            float x = sceneView.position.width - width - xpadding;
            float y = sceneView.position.height - height - ypadding;

            var windowOverlayRect = new Rect(x, y, width, height);
            GUILayout.Window("Animation Playback".GetHashCode(), windowOverlayRect, DoWindow, "Animation Tools");
        }

        public static void ShowPlayer()
        {
            if (!isShown)
            {
                SceneView.duringSceneGui += AnimPlayerWindow.OnSceneGUI;
                isShown = true;
            }
            else            
                Debug.Log("AnimPlayerWindow already open - no need for new delegate");
        }
        
        public static void HidePlayer()
        {
            if (isShown)
            {
                SceneView.duringSceneGui -= AnimPlayerWindow.OnSceneGUI;               
                FacialMorphIMGUI.CleanUp();
                
                isShown = false;
            }
            else
                Debug.Log("AnimPlayerWindow not open - no need to remove delegate");
        }
        
        public static void DoWindow(int id)
        {
            AnimPlayerIMGUI.DrawPlayer();
            FacialMorphIMGUI.DrawFacialMorph();
        }
    }
}
