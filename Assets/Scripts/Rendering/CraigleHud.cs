using UnityEngine;

// Quick GUI for this test
namespace Craigles
{
    public class CraigleHud : MonoBehaviour
    {
        private const float FpsUpdateInterval = 0.1f;

        [SerializeField] private CraigleSimulation simulation;

        private GUIStyle style;
        private GUIStyle fpsStyle;

        private float fpsTimer;
        private int fpsFrames;
        private float fps;

        private void Awake()
        {
            if (simulation == null) simulation = GetComponent<CraigleSimulation>();
        }

        private void Update()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < FpsUpdateInterval) return;

            fps = fpsFrames / fpsTimer;
            fpsFrames = 0;
            fpsTimer = 0f;
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };
            fpsStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

            int count = simulation.Craigles.Count;
            // Unity docs for Graphics.DrawMeshInstanced says that it cannot render more than 1023 instances at once.
            style.normal.textColor = count > 1023 ? Color.red : count > 500 ? Color.yellow : Color.white;

            GUI.Label(new Rect(10, 10, 300, 30), $"Active entities: {count}", style);
            GUI.Label(new Rect(10, 40, 300, 30), $"FPS: {fps:F0}", fpsStyle);
        }
    }
}
