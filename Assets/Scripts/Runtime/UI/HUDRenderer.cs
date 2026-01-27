/*
 * HUDRenderer.cs - HUD 시각화 렌더러
 *
 * [역할] RTOS HUDTask에서 처리된 데이터를 화면에 렌더링
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 *
 * [설계 의도]
 * - HUDTask(RTOS)가 데이터 처리
 * - HUDRenderer(Unity)가 렌더링만 담당
 * - 관심사 분리 (Separation of Concerns)
 */

using RTOScope.RTOS.Tasks;
using UnityEngine;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// 전투기 스타일 HUD 렌더러
    /// OnGUI를 사용한 벡터 스타일 HUD
    /// </summary>
    public class HUDRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Bootstrap.RTOSRunner _runner;

        [Header("Display Settings")]
        [SerializeField] private bool _showHUD = true;
        [SerializeField] private Color _hudColor = new Color(0f, 1f, 0f, 0.9f);
        [SerializeField] private int _fontSize = 14;

        private HUDTask _hudTask;
        private HUDData _data;
        private GUIStyle _labelStyle;
        private GUIStyle _centerStyle;
        private Texture2D _lineTexture;

        // 화면 중심 좌표
        private float _centerX;
        private float _centerY;

        private void Start()
        {
            CreateLineTexture();
        }

        private void Update()
        {
            // HUDTask 참조 가져오기
            if (_hudTask == null && _runner != null && _runner.Kernel != null)
            {
                var tasks = _runner.Kernel.GetAllTasks();
                foreach (var tcb in tasks)
                {
                    if (tcb.Task is HUDTask hud)
                    {
                        _hudTask = hud;
                        break;
                    }
                }
            }

            if (_hudTask != null)
            {
                _data = _hudTask.Data;
            }
        }

        private void OnGUI()
        {
            if (!_showHUD || _data == null) return;

            InitStyles();
            _centerX = Screen.width / 2f;
            _centerY = Screen.height / 2f;

            GUI.color = _hudColor;

            DrawHeadingScale();      // 상단 방위각
            DrawSpeedScale();        // 좌측 속도계
            DrawAltitudeScale();     // 우측 고도계
            DrawPitchLadder();       // 중앙 피치 래더
            DrawCenterReticle();     // 중앙 조준점
            DrawBottomInfo();        // 하단 정보
        }

        private void InitStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = _fontSize,
                    normal = { textColor = _hudColor },
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (_centerStyle == null)
            {
                _centerStyle = new GUIStyle(_labelStyle)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private void CreateLineTexture()
        {
            _lineTexture = new Texture2D(1, 1);
            _lineTexture.SetPixel(0, 0, Color.white);
            _lineTexture.Apply();
        }

        // =====================================================================
        // 방위각 (Heading) - 상단
        // =====================================================================
        private void DrawHeadingScale()
        {
            float y = 50;
            float width = 300;
            float left = _centerX - width / 2;

            // 배경 박스
            DrawBox(left - 10, y - 25, width + 20, 40);

            // 현재 방위각
            float heading = _data.Heading;

            // 눈금 그리기 (10도 간격)
            for (int i = -3; i <= 3; i++)
            {
                float angle = Mathf.Round(heading / 10) * 10 + i * 10;
                float offset = (angle - heading) * 3; // 픽셀 오프셋

                float x = _centerX + offset;
                if (x < left || x > left + width) continue;

                // 눈금선
                DrawLine(x, y - 10, x, y);

                // 숫자 (30도마다)
                if (Mathf.Abs(angle % 30) < 0.1f)
                {
                    int displayAngle = (int)angle;
                    while (displayAngle < 0) displayAngle += 360;
                    while (displayAngle >= 360) displayAngle -= 360;

                    string label = (displayAngle / 10).ToString();
                    GUI.Label(new Rect(x - 15, y - 25, 30, 20), label, _centerStyle);
                }
            }

            // 중앙 마커
            DrawLine(_centerX, y + 5, _centerX, y + 15);
        }

        // =====================================================================
        // 속도계 - 좌측
        // =====================================================================
        private void DrawSpeedScale()
        {
            float x = 80;
            float height = 200;
            float top = _centerY - height / 2;

            // 현재 속도
            float speed = _data.Airspeed;

            // 눈금 그리기 (10노트 간격)
            for (int i = -5; i <= 5; i++)
            {
                float value = Mathf.Round(speed / 10) * 10 + i * 10;
                float offset = (speed - value) * 2;

                float y = _centerY + offset + i * 20;
                if (y < top || y > top + height) continue;

                // 눈금선
                DrawLine(x, y, x + 15, y);

                // 숫자 (50노트마다)
                if (Mathf.Abs(value % 50) < 0.1f && value > 0)
                {
                    GUI.Label(new Rect(x - 50, y - 10, 45, 20), ((int)value).ToString(), _labelStyle);
                }
            }

            // 현재 속도 표시 박스
            DrawBox(x - 55, _centerY - 12, 55, 24);
            GUI.Label(new Rect(x - 52, _centerY - 10, 50, 20), ((int)speed).ToString(), _labelStyle);

            // 화살표
            DrawLine(x, _centerY, x + 20, _centerY);
        }

        // =====================================================================
        // 고도계 - 우측
        // =====================================================================
        private void DrawAltitudeScale()
        {
            float x = Screen.width - 80;
            float height = 200;
            float top = _centerY - height / 2;

            // 현재 고도
            float alt = _data.Altitude;

            // 눈금 그리기 (100피트 간격)
            for (int i = -5; i <= 5; i++)
            {
                float value = Mathf.Round(alt / 100) * 100 + i * 100;
                float offset = (alt - value) * 0.2f;

                float y = _centerY + offset + i * 20;
                if (y < top || y > top + height) continue;

                // 눈금선
                DrawLine(x - 15, y, x, y);

                // 숫자 (500피트마다)
                if (Mathf.Abs(value % 500) < 0.1f && value >= 0)
                {
                    var style = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleRight };
                    GUI.Label(new Rect(x + 5, y - 10, 60, 20), ((int)value).ToString(), _labelStyle);
                }
            }

            // 현재 고도 표시 박스
            DrawBox(x + 5, _centerY - 12, 65, 24);
            GUI.Label(new Rect(x + 8, _centerY - 10, 60, 20), ((int)alt).ToString(), _labelStyle);

            // 화살표
            DrawLine(x - 20, _centerY, x, _centerY);
        }

        // =====================================================================
        // 피치 래더 - 중앙
        // =====================================================================
        private void DrawPitchLadder()
        {
            float pitch = _data.Pitch;
            float roll = _data.Roll;

            // 롤 회전 적용을 위한 매트릭스 저장
            Matrix4x4 matrixBackup = GUI.matrix;

            // 롤 회전 중심
            GUIUtility.RotateAroundPivot(-roll, new Vector2(_centerX, _centerY));

            // 피치 래더 선 (5도 간격)
            for (int deg = -20; deg <= 20; deg += 5)
            {
                if (deg == 0) continue; // 수평선은 따로

                float offset = (pitch - deg) * 4; // 1도당 4픽셀
                float y = _centerY + offset;

                float lineWidth = (deg % 10 == 0) ? 80 : 40;
                float gapWidth = 60;

                // 왼쪽 선
                DrawLine(_centerX - gapWidth - lineWidth, y, _centerX - gapWidth, y);
                // 오른쪽 선
                DrawLine(_centerX + gapWidth, y, _centerX + gapWidth + lineWidth, y);

                // 숫자 (10도마다)
                if (deg % 10 == 0)
                {
                    GUI.Label(new Rect(_centerX - gapWidth - lineWidth - 25, y - 10, 25, 20),
                        Mathf.Abs(deg).ToString(), _centerStyle);
                    GUI.Label(new Rect(_centerX + gapWidth + lineWidth, y - 10, 25, 20),
                        Mathf.Abs(deg).ToString(), _centerStyle);
                }
            }

            // 수평선 (0도)
            float horizonY = _centerY + pitch * 4;
            DrawLine(_centerX - 150, horizonY, _centerX - 60, horizonY);
            DrawLine(_centerX + 60, horizonY, _centerX + 150, horizonY);

            // 매트릭스 복원
            GUI.matrix = matrixBackup;
        }

        // =====================================================================
        // 중앙 조준점
        // =====================================================================
        private void DrawCenterReticle()
        {
            float size = 8;

            // 중앙 원
            DrawLine(_centerX - size, _centerY, _centerX - 3, _centerY);
            DrawLine(_centerX + 3, _centerY, _centerX + size, _centerY);
            DrawLine(_centerX, _centerY - size, _centerX, _centerY - 3);
            DrawLine(_centerX, _centerY + 3, _centerX, _centerY + size);

            // 날개 표시
            DrawLine(_centerX - 40, _centerY, _centerX - 20, _centerY);
            DrawLine(_centerX - 40, _centerY, _centerX - 40, _centerY + 10);

            DrawLine(_centerX + 20, _centerY, _centerX + 40, _centerY);
            DrawLine(_centerX + 40, _centerY, _centerX + 40, _centerY + 10);
        }

        // =====================================================================
        // 하단 정보
        // =====================================================================
        private void DrawBottomInfo()
        {
            float y = Screen.height - 60;

            // G-Force
            GUI.Label(new Rect(50, y, 80, 20), $"G: {_data.GForce:F1}", _labelStyle);

            // 스로틀
            GUI.Label(new Rect(150, y, 100, 20), $"THR: {_data.Throttle:F0}%", _labelStyle);

            // 연료
            GUI.Label(new Rect(270, y, 100, 20), $"FUEL: {_data.FuelPercent:F0}%", _labelStyle);

            // 엔진 온도 (과열 시 색상 변경)
            Color prevColor = GUI.color;
            if (_data.OverheatCritical)
                GUI.color = Color.red;
            else if (_data.OverheatWarning)
                GUI.color = Color.yellow;

            string tempStr = $"EGT: {_data.EngineTemp:F0}°C";
            if (_data.ThrustLimitScale < 1f)
                tempStr += $" [{_data.ThrustLimitScale:P0}]";
            GUI.Label(new Rect(390, y, 150, 20), tempStr, _labelStyle);

            GUI.color = prevColor;

            // 수직속도
            string vsSign = _data.VerticalSpeed >= 0 ? "+" : "";
            GUI.Label(new Rect(Screen.width - 150, y, 120, 20),
                $"VS: {vsSign}{_data.VerticalSpeed:F0} ft/m", _labelStyle);
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================
        private void DrawLine(float x1, float y1, float x2, float y2)
        {
            if (_lineTexture == null) return;

            float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
            float length = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));

            Matrix4x4 matrixBackup = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
            GUI.DrawTexture(new Rect(x1, y1 - 1, length, 2), _lineTexture);
            GUI.matrix = matrixBackup;
        }

        private void DrawBox(float x, float y, float w, float h)
        {
            DrawLine(x, y, x + w, y);           // 상
            DrawLine(x, y + h, x + w, y + h);   // 하
            DrawLine(x, y, x, y + h);           // 좌
            DrawLine(x + w, y, x + w, y + h);   // 우
        }

        /// <summary>HUD 표시 토글</summary>
        public void ToggleHUD()
        {
            _showHUD = !_showHUD;
        }

        public bool IsHUDVisible => _showHUD;
    }
}
