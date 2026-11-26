using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SonarCrew.Multiplayer;

namespace SonarCrew.Core
{
    public enum GameState
    {
        Boot,
        Playing,
        Victory,
        Defeat
    }

    /// <summary>
    /// Arranque y estado global de la partida. Activa tantos PlayerSlot como
    /// gamepads haya conectados (1-4), les aplica el layout de pantalla dividida y
    /// carga de forma aditiva el interior del submarino (`SubmarineInterior`, la
    /// escena de B) sin tocar `Bootstrap`, que es de A.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerSlot[] playerSlots = new PlayerSlot[4];

        [Header("Escenas")]
        [Tooltip("Escena aditiva con el casco y las salas (propiedad de B).")]
        [SerializeField] private string interiorSceneName = "SubmarineInterior";
        [SerializeField] private bool loadInteriorOnStart = true;

        public GameState State { get; private set; } = GameState.Boot;
        public int ActivePlayerCount { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register(this);
            if (inputRouter != null)
                ServiceLocator.Register(inputRouter);
        }

        private void Start()
        {
            if (loadInteriorOnStart)
                LoadInterior();

            if (inputRouter == null)
            {
                Debug.LogError("GameFlow: falta la referencia a InputRouter.");
                return;
            }
            inputRouter.AssignSlots(playerSlots);
            inputRouter.DeviceListChanged += RefreshSlots;
            RefreshSlots();
            State = GameState.Playing;
        }

        private void OnDestroy()
        {
            if (inputRouter != null)
                inputRouter.DeviceListChanged -= RefreshSlots;
        }

        /// <summary>Carga el interior una sola vez y de forma aditiva.</summary>
        private void LoadInterior()
        {
            if (string.IsNullOrEmpty(interiorSceneName)) return;

            Scene existing = SceneManager.GetSceneByName(interiorSceneName);
            if (existing.IsValid() && existing.isLoaded) return;

            if (!Application.CanStreamedLevelBeLoaded(interiorSceneName))
            {
                Debug.LogWarning("GameFlow: la escena '" + interiorSceneName +
                                 "' no está en Build Settings; el interior no se cargará.");
                return;
            }

            SceneManager.LoadSceneAsync(interiorSceneName, LoadSceneMode.Additive);
        }

        /// <summary>Activa los slots 0..N-1 según los gamepads conectados y refresca los rects.</summary>
        private void RefreshSlots()
        {
            int active = Mathf.Clamp(Gamepad.all.Count, 0, playerSlots.Length);
            ActivePlayerCount = active;
            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i] == null) continue;
                bool on = i < active;
                if (playerSlots[i].IsActive != on)
                {
                    playerSlots[i].SetActive(on);
                    if (on) GameEvents.RaisePlayerActivated(i);
                    else GameEvents.RaisePlayerDeactivated(i);
                }
                if (on)
                    playerSlots[i].CameraRig.ApplyLayout(i, active);
            }
        }
    }
}
