using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SonarCrew.Core;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Reparte los gamepads conectados por índice de slot: el slot i usa Gamepad.all[i].
    /// No hay soporte de teclado: cada slot lee los controles del mando que tiene
    /// asignado.
    /// Los controles se leen del <see cref="Gamepad"/> directamente en vez de crear
    /// un <c>InputAction</c> por slot y por mando: crear acciones en cada reconexión
    /// las acumulaba en el estado global del Input System y acababa lanzando
    /// "Control count per binding cannot exceed byte.MaxValue", que además dejaba
    /// los mandos sin actualizar.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 4;

        [Tooltip("Zona muerta de los sticks. Sin ella, la deriva del mando mueve al tripulante solo.")]
        [SerializeField] private float stickDeadzone = 0.2f;

        [Header("Depuración (solo lectura, slot 0)")]
        [SerializeField] private float debugThrustForward;
        [SerializeField] private float debugThrustBackward;
        [SerializeField] private float debugWheel;
        [SerializeField] private Vector2 debugMove;

        private readonly PlayerSlot[] _slots = new PlayerSlot[4];
        private readonly Gamepad[] _pads = new Gamepad[4];

        /// <summary>Se lanza cuando cambia el número de gamepads asignados.</summary>
        public event Action DeviceListChanged;

        public int MaxPlayers => maxPlayers;
        public int AssignedCount { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Update()
        {
            // Solo lectura en el Inspector durante Play, para el slot 0.
            debugThrustForward = GetThrustForward(0);
            debugThrustBackward = GetThrustBackward(0);
            debugWheel = GetWheel(0);
            debugMove = GetMove(0);
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            RefreshDevices();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            for (int i = 0; i < _pads.Length; i++)
                _pads[i] = null;
            AssignedCount = 0;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad)
                RefreshDevices();
        }

        /// <summary>Registra los slots de la escena y fuerza el reparto de dispositivos.</summary>
        public void AssignSlots(PlayerSlot[] slots)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = slots != null && i < slots.Length ? slots[i] : null;
            RefreshDevices();
        }

        /// <summary>Reasigna Gamepad.all[i] al slot i (hasta maxPlayers).</summary>
        public void RefreshDevices()
        {
            int count = Mathf.Min(Gamepad.all.Count, maxPlayers);
            AssignedCount = count;

            for (int i = 0; i < maxPlayers; i++)
            {
                Gamepad pad = i < count ? Gamepad.all[i] : null;
                if (_pads[i] == pad) continue;

                _pads[i] = pad;
                if (_slots[i] != null)
                    _slots[i].Setup(i, pad);
                if (pad != null)
                    GameEvents.RaisePlayerDeviceAssigned(i, pad);
            }

            DeviceListChanged?.Invoke();
        }

        /// <summary>
        /// Mando del slot, solo si sigue conectado. Un dispositivo retirado no se
        /// puede leer: lanzaría MissingReferenceException.
        /// </summary>
        private Gamepad Pad(int slot)
        {
            if (slot < 0 || slot >= _pads.Length) return null;
            Gamepad pad = _pads[slot];
            return pad != null && pad.added ? pad : null;
        }

        /// <summary>
        /// Zona muerta radial: con el mando parado el tripulante no debe moverse ni
        /// girar. Lo que supera la zona muerta se reescala para no perder recorrido.
        /// </summary>
        private Vector2 ApplyDeadzone(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude <= stickDeadzone) return Vector2.zero;
            return value * ((magnitude - stickDeadzone) / (magnitude * (1f - stickDeadzone)));
        }

        /// <summary>Zona muerta para un eje suelto (timón, palanca).</summary>
        private float ApplyDeadzone(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= stickDeadzone) return 0f;
            return Mathf.Sign(value) * ((magnitude - stickDeadzone) / (1f - stickDeadzone));
        }

        public Vector2 GetMove(int slot)
        {
            Gamepad pad = Pad(slot);
            if (pad == null) return Vector2.zero;

            // Stick izquierdo o cruceta, como los dos composites del hito 0.
            Vector2 stick = ApplyDeadzone(pad.leftStick.ReadValue());
            Vector2 dpad = pad.dpad.ReadValue();
            return Vector2.ClampMagnitude(stick + dpad, 1f);
        }

        public Vector2 GetLook(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null ? ApplyDeadzone(pad.rightStick.ReadValue()) : Vector2.zero;
        }

        public bool GetInteractPressed(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.buttonNorth.wasPressedThisFrame;
        }

        public bool GetInteractHeld(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.buttonNorth.isPressed;
        }

        public bool GetJumpPressed(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        public bool GetSprintHeld(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.leftStickButton.isPressed;
        }

        public float GetWheel(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null ? ApplyDeadzone(pad.rightStick.x.ReadValue()) : 0f;
        }

        public float GetThrustForward(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null ? pad.rightTrigger.ReadValue() : 0f;
        }

        public float GetThrustBackward(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null ? pad.leftTrigger.ReadValue() : 0f;
        }

        public bool GetAscendHeld(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.dpad.up.isPressed;
        }

        public bool GetDescendHeld(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.dpad.down.isPressed;
        }

        // --- Botones de estación (hito 2, persona B) ---

        /// <summary>Botón oeste: accionar la estación que se está operando.</summary>
        public bool GetAttackPressed(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.buttonWest.wasPressedThisFrame;
        }

        public bool GetAttackHeld(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.buttonWest.isPressed;
        }

        public bool GetPreviousPressed(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.dpad.left.wasPressedThisFrame;
        }

        public bool GetNextPressed(int slot)
        {
            Gamepad pad = Pad(slot);
            return pad != null && pad.dpad.right.wasPressedThisFrame;
        }
    }
}
