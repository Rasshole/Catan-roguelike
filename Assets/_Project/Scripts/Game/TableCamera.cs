using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class TableCamera : MonoBehaviour
    {
        [SerializeField] private Transform lookTarget;
        [SerializeField] private BoardView boardView;
        [SerializeField] private float distanceMargin = TableCameraFraming.DefaultMarginFactor;
        [SerializeField] private float orbitSpeed = 40f;
        [SerializeField] private bool frameBoardRightOfHud = true;

        private float _angle;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (boardView == null)
                boardView = FindFirstObjectByType<BoardView>();
        }

        private void Start()
        {
            if (lookTarget == null)
            {
                var target = new GameObject("BoardCenter");
                lookTarget = target.transform;
            }

            ApplyOrbitPose();
            if (frameBoardRightOfHud && _camera != null)
                ApplyBoardFramingOffset();
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.Q))
                _angle -= orbitSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _angle += orbitSpeed * Time.deltaTime;

            ApplyOrbitPose();
            if (frameBoardRightOfHud && _camera != null)
                ApplyBoardFramingOffset();
        }

        private void ApplyOrbitPose()
        {
            float boardRadius = boardView != null ? boardView.GetBoardBoundingRadius() : 0f;
            float distance = TableCameraFraming.ComputeOrbitDistance(boardRadius, distanceMargin);
            float height = TableCameraFraming.ComputeOrbitHeight(distance);

            var offset = Quaternion.Euler(55f, _angle, 0f) * new Vector3(0f, 0f, -distance);
            transform.position = lookTarget.position + offset + Vector3.up * (height * 0.3f);
            transform.LookAt(lookTarget.position + Vector3.up * 0.5f);
        }

        private void ApplyBoardFramingOffset()
        {
            float desiredScreenX = Screen.width * 0.5f
                + PlaceholderHudLayout.GetBoardScreenOffsetX(PlaceholderHudLayout.LastPanelWidth, Screen.width);
            Vector3 boardScreen = _camera.WorldToScreenPoint(lookTarget.position);
            float deltaPx = desiredScreenX - boardScreen.x;
            if (Mathf.Abs(deltaPx) <= 0.5f)
                return;

            Vector3 worldAtBoard = _camera.ScreenToWorldPoint(
                new Vector3(boardScreen.x, boardScreen.y, boardScreen.z));
            Vector3 worldShifted = _camera.ScreenToWorldPoint(
                new Vector3(boardScreen.x + deltaPx, boardScreen.y, boardScreen.z));
            // Move camera opposite to the desired on-screen board shift (LookAt(+delta) inverts).
            transform.position -= worldShifted - worldAtBoard;
            transform.LookAt(lookTarget.position + Vector3.up * 0.5f);
        }
    }
}
