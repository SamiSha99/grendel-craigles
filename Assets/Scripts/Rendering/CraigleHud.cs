using UnityEngine;

// Quick GUI for this test
namespace Craigles
{
    public class CraigleHud : MonoBehaviour
    {
        [SerializeField] private CraigleSimulation simulation;

        private GUIStyle style;

        private void Awake()
        {
            if (simulation == null) simulation = GetComponent<CraigleSimulation>();
        }

        private void OnGUI()
        {
            if (style == null)
            {
                Color textColor = Color.white;
                // Unity docs for Graphics.DrawMeshInstanced says that it cannot render more than 1023 instances at once.
                if(simulation.Craigles.Count > 1023)
                    textColor = Color.red;
                else if(simulation.Craigles.Count > 500)
                    textColor = Color.yellow;
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = textColor } };
            }

            GUI.Label(new Rect(10, 10, 300, 30), $"Active entities: {simulation.Craigles.Count}", style);
        }
    }
}
