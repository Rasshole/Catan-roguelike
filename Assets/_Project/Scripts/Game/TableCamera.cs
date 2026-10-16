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

        /// <summary>Default orbit yaw (0 = camera on −Z, same as Play Mode start).</summary>
        public const float DefaultOrbitYawDegrees = 0f;

        private float _angle = DefaultOrbitYawDegrees;
        private Camera _camera;

        private void Awake()
        {
            EnsureCameraAndBoardView();
        }

        private void Start()
        {
            EnsureLookTarget();
            if (ColonistPresentation.IsActive)
                ApplyColonistTopDownPose();
            else
            {
                ApplyOrbitPose();
                if (frameBoardRightOfHud && _camera != null)
                    ApplyBoardFramingOffset();
            }
        }

        /// <summary>Apply play/capture camera pose for the given render size (PlayMode PNG + editor capture).</summary>
        public void ApplyCapturePose(int screenWidth, int screenHeight)
        {
            EnsureCameraAndBoardView();
            EnsureLookTarget();
            _angle = DefaultOrbitYawDegrees;

            if (ColonistPresentation.IsActive)
                ApplyColonistTopDownPose(screenWidth, screenHeight);
            else
            {
                ApplyOrbitPose();
                if (frameBoardRightOfHud && _camera != null)
                    ApplyBoardFramingOffset(screenWidth, screenHeight);
            }
        }

#if UNITY_EDITOR
        public void ApplyPoseForCapture(int screenWidth, int screenHeight)
        {
            var previousPixelRect = _camera != null ? _camera.pixelRect : default;
            if (_camera != null && screenWidth > 0 && screenHeight > 0)
                _camera.pixelRect = new Rect(0f, 0f, screenWidth, screenHeight);

            try
            {
                ApplyCapturePose(screenWidth, screenHeight);
            }
            finally
            {
                if (_camera != null)
                    _camera.pixelRect = previousPixelRect;
            }
        }
#endif

        private void EnsureCameraAndBoardView()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
                if (_camera != null)
                {
                    if (_camera.nearClipPlane < 0.3f)
                        _camera.nearClipPlane = 0.3f;
                    if (ColonistPresentation.IsActive)
                        _camera.backgroundColor = new Color(0.08f, 0.22f, 0.38f, 1f);
                }
            }

            if (boardView == null)
                boardView = FindFirstObjectByType<BoardView>();
        }

        private void EnsureLookTarget()
        {
            if (lookTarget == null)
            {
                var target = new GameObject("BoardCenter");
                lookTarget = target.transform;
            }
        }

        private void Update()
        {
            if (ColonistPresentation.IsActive)
            {
                ApplyColonistTopDownPose();
                return;
            }

            if (Input.GetKey(KeyCode.Q))
                _angle -= orbitSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _angle += orbitSpeed * Time.deltaTime;

            ApplyOrbitPose();
            if (frameBoardRightOfHud && _camera != null)
                ApplyBoardFramingOffset();
        }

        private void ApplyColonistTopDownPose(int screenWidth = 0, int screenHeight = 0)
        {
            if (_camera == null)
                return;

            if (screenWidth <= 0)
                screenWidth = Screen.width;
            if (screenHeight <= 0)
                screenHeight = Screen.height;

            float boardRadius = boardView != null ? boardView.GetBoardBoundingRadius() : 0f;
            if (boardRadius <= 0f)
                boardRadius = 4f;

            _camera.orthographic = true;
            _camera.aspect = screenWidth / (float)screenHeight;
            transform.rotation = Quaternion.Euler(90f, ColonistBoardViewConstants.CameraYawDegrees, 0f);

            float left = RunHudLayout.GetContentLeftInset();
            float right = RunHudLayout.GetContentRightInset();
            float bottom = RunHudLayout.GetBottomHandHeight()
                + RunHudLayout.GetBottomActionBarHeight()
                + RunHudLayout.Margin * 2f;
            float top = RunHudLayout.Margin;

            float playWidth = Mathf.Max(64f, screenWidth - left - right);
            float playHeight = Mathf.Max(64f, screenHeight - bottom - top);
            float boardFill = ColonistBoardViewConstants.BoardPlayAreaFill;
            float orthoFromHeight = boardRadius * screenHeight / (boardFill * playHeight);
            float orthoFromWidth = boardRadius * screenWidth / (boardFill * playWidth * _camera.aspect);
            _camera.orthographicSize = Mathf.Max(orthoFromHeight, orthoFromWidth);

            float worldPerPixel = (2f * _camera.orthographicSize) / screenHeight;
            float desiredScreenX = left + playWidth * 0.5f;
            float desiredScreenY = bottom + playHeight * 0.5f;
            float deltaScreenX = desiredScreenX - screenWidth * 0.5f;
            float deltaScreenY = desiredScreenY - screenHeight * 0.5f;

            Vector3 center = boardView != null ? boardView.transform.position : lookTarget.position;
            var rightAxis = transform.right;
            rightAxis.y = 0f;
            rightAxis.Normalize();
            var upAxis = transform.up;
            upAxis.y = 0f;
            upAxis.Normalize();
            Vector3 pan = rightAxis * (deltaScreenX * worldPerPixel) + upAxis * (deltaScreenY * worldPerPixel);
            transform.position = new Vector3(center.x, center.y + 28f, center.z) - pan;
        }

        private void ApplyOrbitPose()
        {
            float boardRadius = boardView != null ? boardView.GetBoardBoundingRadius() : 0f;
            float distance = TableCameraFraming.ComputeOrbitDistance(boardRadius, distanceMargin);
            float height = TableCameraFraming.ComputeOrbitHeight(distance);

            var offset = Quaternion.Euler(64f, _angle, 0f) * new Vector3(0f, 0f, -distance);
            transform.position = lookTarget.position + offset + Vector3.up * (height * 0.22f);
            transform.LookAt(lookTarget.position + Vector3.up * 0.5f);
        }

        private void ApplyBoardFramingOffset(int screenWidth = 0, int screenHeight = 0)
        {
            if (screenWidth <= 0)
                screenWidth = Screen.width;
            if (screenHeight <= 0)
                screenHeight = Screen.height;

            float desiredScreenX = screenWidth * 0.5f
                + PlaceholderHudLayout.GetBoardScreenOffsetX(
                    PlaceholderHudLayout.LastPanelWidth,
                    screenWidth,
                    RunHudLayout.RightChromePanelWidth);
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
            ApplyVerticalBoardCentering(screenHeight);
        }

        private void ApplyVerticalBoardCentering(int screenHeight)
        {
            if (_camera == null || boardView == null || screenHeight <= 0)
                return;

            float boardRadius = boardView.GetBoardBoundingRadius();
            if (boardRadius <= 0f)
                return;

            Vector3 center = lookTarget.position;
            float topY = _camera.WorldToScreenPoint(center + Vector3.up * boardRadius).y;
            float bottomY = _camera.WorldToScreenPoint(center - Vector3.up * (boardRadius * 0.35f)).y;
            float midY = (topY + bottomY) * 0.5f;
            float desiredMidY = screenHeight * 0.54f;
            float deltaY = desiredMidY - midY;
            if (Mathf.Abs(deltaY) <= 0.5f)
                return;

            Vector3 boardScreen = _camera.WorldToScreenPoint(center);
            Vector3 worldAtMid = _camera.ScreenToWorldPoint(
                new Vector3(boardScreen.x, midY, boardScreen.z));
            Vector3 worldShifted = _camera.ScreenToWorldPoint(
                new Vector3(boardScreen.x, midY + deltaY, boardScreen.z));
            transform.position -= worldShifted - worldAtMid;
            transform.LookAt(lookTarget.position + Vector3.up * 0.5f);
        }
    }
}
