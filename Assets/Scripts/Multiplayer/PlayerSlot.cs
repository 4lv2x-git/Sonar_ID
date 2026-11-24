using UnityEngine;
using UnityEngine.InputSystem;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Un slot de jugador local: índice, gamepad asignado, cámara (CameraRig)
    /// y tripulante (CrewController). El gamepad se reparte por índice.
    /// </summary>
    public class PlayerSlot : MonoBehaviour
    {
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private CrewController crew;

        public int Index { get; private set; } = -1;
        public Gamepad Gamepad { get; private set; }
        public CameraRig CameraRig => cameraRig;
        public CrewController Crew => crew;
        public bool IsActive { get; private set; }

        public void Setup(int index, Gamepad gamepad)
        {
            Index = index;
            Gamepad = gamepad;
            if (crew != null)
                crew.Bind(this);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (gameObject.activeSelf != active)
                gameObject.SetActive(active);
        }
    }
}
