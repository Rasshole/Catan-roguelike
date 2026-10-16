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
                        _camera.backgroundColor = ColonistBoardSurfaces.OceanClearColor;
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

            var framingSamples = new System.Collections.Generic.List<Vector3>(512);
            if (boardView != null)
                boardView.FillColonistFramingSamples(framingSamples);

            _camera.orthographic = true;
            _camera.aspect = screenWidth / (float)Mathf.Max(1, screenHeight);
            transform.rotation = Quaternion.Euler(90f, ColonistBoardViewConstants.CameraYawDegrees, 0f);

            var fit = ColonistBoardFraming.ComputeColonistCameraFit(framingSamples, screenWidth, screenHeight);
            _camera.orthographicSize = fit.OrthographicSize;
            transform.position = fit.Position;
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
