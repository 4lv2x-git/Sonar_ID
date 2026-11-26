using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SonarCrew.Core;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Conjunto de InputAction de un slot, ya bindeadas al gamepad concreto de ese slot.
    /// </summary>
    public sealed class SlotInput
    {
        public InputAction Move;
        public InputAction Look;
        public InputAction Interact;
        public InputAction Jump;
        public InputAction Sprint;
        public InputAction Attack;
        public InputAction Crouch;
        public InputAction Previous;
        public InputAction Next;
        // Pilotaje del submarino (hito 1): timón y palancas. Hito 2: la estación
        // de mandos gatea por TryOccupy; aquí basta el driver del slot activo.
        public InputAction Wheel;           // timón: yaw (eje x del stick derecho)
        public InputAction ThrustForward;   // palanca avance (gatillo derecho)
        public InputAction ThrustBackward;  // palanca retroceso (gatillo izquierdo)
        public InputAction Ascend;          // palanca ascenso (D-pad arriba)
        public InputAction Descend;         // palanca descenso (D-pad abajo)

        private InputAction[] All()
        {
            return new[] { Move, Look, Interact, Jump, Sprint, Attack, Crouch, Previous, Next,
                Wheel, ThrustForward, ThrustBackward, Ascend, Descend };
        }

        public void Enable()
        {
            foreach (InputAction action in All())
                action?.Enable();
        }

        public void Disable()
        {
            foreach (InputAction action in All())
                action?.Disable();
        }
    }

    /// <summary>
    /// Reparte los gamepads conectados por índice de slot: el slot i usa Gamepad.all[i].
    /// No hay soporte de teclado: cada acción se crea con el path del dispositivo asignado.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 4;

        [Header("Depuración (solo lectura, slot 0)")]
        [SerializeField] private float debugThrustForward;
        [SerializeField] private float debugThrustBackward;
        [SerializeField] private float debugWheel;
        [SerializeField] private Vector2 debugMove;

        private readonly PlayerSlot[] _slots = new PlayerSlot[4];
        private readonly SlotInput[] _inputs = new SlotInput[4];
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
            for (int i = 0; i < _inputs.Length; i++)
            {
                _inputs[i]?.Disable();
                _inputs[i] = null;
                _pads[i] = null;
            }
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
                if (i < count)
                {
                    Gamepad pad = Gamepad.all[i];
                    if (_pads[i] != pad || _inputs[i] == null)
                    {
                        _pads[i] = pad;
                        _inputs[i]?.Disable();
                        _inputs[i] = BuildSlotInput(pad);
                        if (_slots[i] != null)
                            _slots[i].Setup(i, pad);
                        GameEvents.RaisePlayerDeviceAssigned(i, pad);
                    }
                }
                else if (_pads[i] != null || _inputs[i] != null)
                {
                    _pads[i] = null;
                    _inputs[i]?.Disable();
                    _inputs[i] = null;
                    if (_slots[i] != null)
                        _slots[i].Setup(i, null);
                }
            }
            DeviceListChanged?.Invoke();
        }

        /// <summary>Crea las acciones del slot bindeadas al path del gamepad físico asignado.</summary>
        private static SlotInput BuildSlotInput(Gamepad pad)
        {
            string p = pad.path;
            var input = new SlotInput();

            input.Move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            input.Move.AddCompositeBinding("2DVector")
                .With("up", p + "/leftStick/up")
                .With("down", p + "/leftStick/down")
                .With("left", p + "/leftStick/left")
                .With("right", p + "/leftStick/right");
            input.Move.AddCompositeBinding("2DVector")
                .With("up", p + "/dpad/up")
                .With("down", p + "/dpad/down")
                .With("left", p + "/dpad/left")
                .With("right", p + "/dpad/right");

            input.Look = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            input.Look.AddCompositeBinding("2DVector")
                .With("up", p + "/rightStick/up")
                .With("down", p + "/rightStick/down")
                .With("left", p + "/rightStick/left")
                .With("right", p + "/rightStick/right");

            input.Interact = new InputAction("Interact", InputActionType.Button, binding: p + "/buttonNorth");
            input.Jump = new InputAction("Jump", InputActionType.Button, binding: p + "/buttonSouth");
            input.Sprint = new InputAction("Sprint", InputActionType.Button, binding: p + "/leftStickPress");
            input.Attack = new InputAction("Attack", InputActionType.Button, binding: p + "/buttonWest");
            input.Crouch = new InputAction("Crouch", InputActionType.Button, binding: p + "/buttonEast");
            input.Previous = new InputAction("Previous", InputActionType.Button, binding: p + "/dpad/left");
            input.Next = new InputAction("Next", InputActionType.Button, binding: p + "/dpad/right");
            input.Wheel = new InputAction("Wheel", InputActionType.Value, expectedControlType: "Axis");
            input.Wheel.AddBinding(p + "/rightStick/x");
            // Palancas continuas: Value + Axis para leer 0..1 analógico del gatillo,
            // igual que el timón (un Button sobre eje analógico solo expone estados cortados).
            input.ThrustForward = new InputAction("ThrustForward", InputActionType.Value, expectedControlType: "Axis");
            input.ThrustForward.AddBinding(p + "/rightTrigger");
            input.ThrustBackward = new InputAction("ThrustBackward", InputActionType.Value, expectedControlType: "Axis");
            input.ThrustBackward.AddBinding(p + "/leftTrigger");
            input.Ascend = new InputAction("Ascend", InputActionType.Button, binding: p + "/dpad/up");
            input.Descend = new InputAction("Descend", InputActionType.Button, binding: p + "/dpad/down");

            input.Enable();
            return input;
        }

        private SlotInput Slot(int slot)
        {
            return slot >= 0 && slot < _inputs.Length ? _inputs[slot] : null;
        }

        public Vector2 GetMove(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Move != null ? input.Move.ReadValue<Vector2>() : Vector2.zero;
        }

        public Vector2 GetLook(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Look != null ? input.Look.ReadValue<Vector2>() : Vector2.zero;
        }

        public bool GetInteractPressed(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Interact != null && input.Interact.WasPressedThisFrame();
        }

        public bool GetInteractHeld(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Interact != null && input.Interact.IsPressed();
        }

        public bool GetJumpPressed(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Jump != null && input.Jump.WasPressedThisFrame();
        }

        public bool GetSprintHeld(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Sprint != null && input.Sprint.IsPressed();
        }

        public float GetWheel(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Wheel != null ? input.Wheel.ReadValue<float>() : 0f;
        }

        public float GetThrustForward(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.ThrustForward != null ? input.ThrustForward.ReadValue<float>() : 0f;
        }

        public float GetThrustBackward(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.ThrustBackward != null ? input.ThrustBackward.ReadValue<float>() : 0f;
        }

        public bool GetAscendHeld(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Ascend != null && input.Ascend.IsPressed();
        }

        public bool GetDescendHeld(int slot)
        {
            SlotInput input = Slot(slot);
            return input != null && input.Descend != null && input.Descend.IsPressed();
        }
    }
}
