using UnityEngine;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    public class DebugStats : MonoBehaviour
    {
        CubeSculptureBootstrap bootstrap;
        GUIStyle style;

        void Start()
        {
            bootstrap = GetComponent<CubeSculptureBootstrap>();
        }

        void OnGUI()
        {
            if (bootstrap == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };

            ref var world = ref bootstrap.World;

            var fps = 1f / Time.unscaledDeltaTime;
            var ms = Time.unscaledDeltaTime * 1000f;
            var entities = world.EntitiesAmount;

            GUILayout.BeginArea(new Rect(10, 10, 320, 160));
            GUILayout.Label($"<b>FPS:</b> {fps:F1}  ({ms:F2} ms)", style);
            GUILayout.Label($"<b>Entities:</b> {entities}", style);
            GUILayout.Label($"<b>Target Count:</b> {bootstrap.TargetCount}", style);
            GUILayout.Label($"<b>Progress:</b> {(float)entities / bootstrap.TargetCount * 100:F1}%", style);
            GUILayout.EndArea();
        }
    }
}
