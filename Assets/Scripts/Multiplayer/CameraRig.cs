using UnityEngine;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Aplica a la cámara del slot su rect de pantalla dividida (Camera.rect)
    /// según cuántos jugadores hay activos, y gestiona el AudioListener.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private AudioListener audioListener;

        public int SlotIndex { get; private set; } = -1;

        public void ApplyLayout(int slotIndex, int activeCount)
        {
            SlotIndex = slotIndex;
            if (cam != null)
            {
                cam.rect = RectFor(slotIndex, activeCount);
                cam.depth = slotIndex;
            }
            // Un único AudioListener activo (el del slot 0) para evitar avisos de Unity.
            if (audioListener != null)
                audioListener.enabled = slotIndex == 0;
        }

        /// <summary>Rect de pantalla para el slot según el número de jugadores activos.</summary>
        public static Rect RectFor(int slotIndex, int activeCount)
        {
            switch (activeCount)
            {
                case 1:
                    return new Rect(0f, 0f, 1f, 1f);
                case 2:
                    return slotIndex == 0
                        ? new Rect(0f, 0f, 0.5f, 1f)
                        : new Rect(0.5f, 0f, 0.5f, 1f);
                case 3:
                    if (slotIndex == 0) return new Rect(0f, 0f, 0.5f, 1f);
                    return slotIndex == 1
                        ? new Rect(0.5f, 0.5f, 0.5f, 0.5f)
                        : new Rect(0.5f, 0f, 0.5f, 0.5f);
                default:
                    int col = slotIndex % 2;
                    int row = slotIndex / 2;
                    return new Rect(col * 0.5f, (1 - row) * 0.5f, 0.5f, 0.5f);
            }
        }
    }
}
