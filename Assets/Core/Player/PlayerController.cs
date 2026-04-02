using UnityEngine;
using UnityEngine.InputSystem;
using StaticDrift.Managers;

namespace StaticDrift.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 180f;
        [SerializeField] private float _acceleration = 10f;
        [SerializeField] private float _maxSpeed = 9f;
        [SerializeField] private float _linearDamping = 0.3f;
        [SerializeField] private float _screenWrapMargin = 0.25f;

        private Rigidbody2D _rigidbody2D;
        private Camera _mainCamera;
        private bool _rotateLeftHeldByUI;
        private bool _rotateRightHeldByUI;
        private bool _accelerateHeldByUI;
        private float _rotateDirectionInput;
        private bool _accelerateInput;
        public bool IsAccelerating => _accelerateInput;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _mainCamera = Camera.main;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.linearDamping = _linearDamping;
            _rigidbody2D.angularDamping = 0f;
        }

        private void Update()
        {
            bool rotateLeft = _rotateLeftHeldByUI;
            bool rotateRight = _rotateRightHeldByUI;
            bool accelerate = _accelerateHeldByUI;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                // L/R shoulder rotate. A button accelerates.
                rotateLeft |= gamepad.leftShoulder.isPressed;
                rotateRight |= gamepad.rightShoulder.isPressed;
                accelerate |= gamepad.buttonSouth.isPressed;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                rotateLeft |= keyboard.aKey.isPressed;
                rotateRight |= keyboard.dKey.isPressed;
                accelerate |= keyboard.wKey.isPressed;
            }

            float direction = 0f;
            if (rotateLeft)
            {
                direction += 1f;
            }
            if (rotateRight)
            {
                direction -= 1f;
            }

            _rotateDirectionInput = direction;
            _accelerateInput = accelerate;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (Mathf.Abs(_rotateDirectionInput) > 0.01f)
            {
                float sensitivity = GameSettings.RotationSensitivity;
                float nextRotation = _rigidbody2D.rotation + (_rotateDirectionInput * _rotationSpeed * sensitivity * dt);
                _rigidbody2D.MoveRotation(nextRotation);
            }

            if (_accelerateInput)
            {
                Vector2 forward = transform.up;
                _rigidbody2D.AddForce(forward * _acceleration, ForceMode2D.Force);
            }

            float maxSpeedSq = _maxSpeed * _maxSpeed;
            Vector2 velocity = _rigidbody2D.linearVelocity;
            if (velocity.sqrMagnitude > maxSpeedSq)
            {
                _rigidbody2D.linearVelocity = velocity.normalized * _maxSpeed;
            }

            WrapAtScreenEdges();
        }

        public void SetRotateLeftHeld(bool isHeld)
        {
            _rotateLeftHeldByUI = isHeld;
        }

        public void SetRotateRightHeld(bool isHeld)
        {
            _rotateRightHeldByUI = isHeld;
        }

        public void SetAccelerateHeld(bool isHeld)
        {
            _accelerateHeldByUI = isHeld;
        }

        private void WrapAtScreenEdges()
        {
            if (_mainCamera == null || !_mainCamera.orthographic)
            {
                return;
            }

            Vector3 camPos = _mainCamera.transform.position;
            float halfHeight = _mainCamera.orthographicSize;
            float halfWidth = halfHeight * _mainCamera.aspect;

            float left = camPos.x - halfWidth;
            float right = camPos.x + halfWidth;
            float bottom = camPos.y - halfHeight;
            float top = camPos.y + halfHeight;

            Vector2 pos = _rigidbody2D.position;
            bool wrapped = false;

            if (pos.x < left - _screenWrapMargin)
            {
                pos.x = right + _screenWrapMargin;
                wrapped = true;
            }
            else if (pos.x > right + _screenWrapMargin)
            {
                pos.x = left - _screenWrapMargin;
                wrapped = true;
            }

            if (pos.y < bottom - _screenWrapMargin)
            {
                pos.y = top + _screenWrapMargin;
                wrapped = true;
            }
            else if (pos.y > top + _screenWrapMargin)
            {
                pos.y = bottom - _screenWrapMargin;
                wrapped = true;
            }

            if (wrapped)
            {
                _rigidbody2D.position = pos;
            }
        }
    }
}
