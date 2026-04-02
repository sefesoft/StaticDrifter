using UnityEngine;
using UnityEngine.EventSystems;
using StaticDrift.Player;

namespace StaticDrift.UI
{
    public class ShipControlButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum ControlKind
        {
            RotateLeft,
            RotateRight,
            Accelerate
        }

        [SerializeField] private ControlKind _controlKind = ControlKind.RotateLeft;

        private PlayerController _playerController;

        private const string PlayerTag = "Player";

        public void Configure(ControlKind controlKind)
        {
            _controlKind = controlKind;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHeld(false);
        }

        private void OnDisable()
        {
            SetHeld(false);
        }

        private void SetHeld(bool isHeld)
        {
            if (_playerController == null)
            {
                TryGetPlayerController();
            }

            if (_playerController == null)
            {
                return;
            }

            if (_controlKind == ControlKind.RotateLeft)
            {
                _playerController.SetRotateLeftHeld(isHeld);
            }
            else if (_controlKind == ControlKind.RotateRight)
            {
                _playerController.SetRotateRightHeld(isHeld);
            }
            else
            {
                _playerController.SetAccelerateHeld(isHeld);
            }
        }

        private void TryGetPlayerController()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerGo == null)
            {
                return;
            }

            _playerController = playerGo.GetComponent<PlayerController>();
            if (_playerController == null)
            {
                _playerController = playerGo.GetComponentInChildren<PlayerController>();
            }
        }
    }
}
