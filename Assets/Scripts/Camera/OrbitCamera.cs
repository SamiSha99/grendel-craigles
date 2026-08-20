using UnityEngine;
using UnityEngine.InputSystem;

namespace Craigles
{
    public class OrbitCamera : MonoBehaviour
    {
        private const float Distance = 50f;

        [Tooltip("Optional. If set, the orbit is centered on this simulation's cube automatically.")]
        [SerializeField] private CraigleSimulation simulation;
        [SerializeField] private Vector3 target = new(50f, 50f, 50f);
        [SerializeField] private float rotateSpeed = 0.2f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 80f;

        private float yaw;
        private float pitch;

        private void Start()
        {
            if (simulation != null && simulation.Config != null)
            {
                float half = simulation.Config.BoundsSize * 0.5f;
                target = new Vector3(half, half, half);
            }

            // Start from wherever the camera is already pointed.
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotateSpeed;
                pitch -= delta.y * rotateSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target + rotation * new Vector3(0f, 0f, -Distance), rotation);
        }
    }
}
