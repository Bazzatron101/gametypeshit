using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterSystem.Game
{
    /// <summary>
    /// Free third-person camera: orbits the target with mouse (or right stick) look and follows
    /// it every frame. The cursor is locked while playing; Escape frees it and a click locks it again.
    /// Presentation only: the simulation never reads the camera. The local input source does,
    /// to make movement camera-relative.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the views have moved this frame
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        /// <summary>Starting values, shared with the scene builder so the editor view matches the first frame.</summary>
        public const float DefaultTargetHeight = 1.4f;
        public const float DefaultDistance = 7f;
        public const float DefaultPitch = 30f;

        [SerializeField] private Transform target;
        [SerializeField, Tooltip("Look-at point above the target's feet, in metres")] private float targetHeight = DefaultTargetHeight;
        [SerializeField, Min(1f)] private float distance = DefaultDistance;
        [SerializeField] private float minPitch = -5f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField, Tooltip("Degrees per pixel of mouse movement")] private float mouseSensitivity = 0.15f;
        [SerializeField, Tooltip("Degrees per second at full stick")] private float stickSensitivity = 180f;

        [SerializeField] private float yaw;
        [SerializeField] private float pitch = DefaultPitch;

        private InputAction _look;

        public Transform Target => target;
        public float Distance => distance;
        public float TargetHeight => targetHeight;

        private readonly System.Collections.Generic.HashSet<object> _openMenus = new System.Collections.Generic.HashSet<object>();

        /// <summary>True while any menu is open: clicks do not re-capture the cursor and the mouse does not orbit.</summary>
        public bool InMenu => _openMenus.Count > 0;

        /// <summary>Frees the cursor for <paramref name="menu"/>. Several menus can be open at once.</summary>
        public void ReleaseCursorForMenu(object menu)
        {
            _openMenus.Add(menu);
            LockCursor(false);
        }

        /// <summary>
        /// Closes <paramref name="menu"/>. The cursor is re-captured only when this was the last open
        /// menu and <paramref name="recapture"/> asks for it.
        /// </summary>
        public void EndMenu(object menu, bool recapture)
        {
            _openMenus.Remove(menu);
            if (!InMenu && recapture && Application.isFocused) LockCursor(true);
        }

        private void Start()
        {
            _look = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Look") : null;
            _look?.Enable();
            if (Application.isFocused && !InMenu) LockCursor(true); // a menu opened first keeps the cursor free
        }

        private void LateUpdate()
        {
            HandleCursorLock();

            if (_look != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = _look.ReadValue<Vector2>();
                // Mouse delta is pixels per frame; stick is -1..1 and needs time scaling.
                var isMouse = _look.activeControl != null && _look.activeControl.device is Pointer;
                var scale = isMouse ? mouseSensitivity : stickSensitivity * Time.deltaTime;
                yaw += delta.x * scale;
                pitch = Mathf.Clamp(pitch - delta.y * scale, minPitch, maxPitch);
            }

            if (target == null) return;
            var focus = target.position + Vector3.up * targetHeight;
            transform.position = CameraMath.OrbitPosition(focus, yaw, pitch, distance);
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }

        private void HandleCursorLock()
        {
            if (InMenu) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable() => LockCursor(false);
    }
}
