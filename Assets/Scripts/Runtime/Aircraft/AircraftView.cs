/*
 * AircraftView.cs - 항공기 시각화 컴포넌트
 * 
 * [역할] 항공기 GameObject의 시각적 표현 관리
 * [위치] Runtime Layer > Aircraft (Unity MonoBehaviour)
 * 
 * [설계 의도]
 * - AircraftState의 데이터를 기반으로 시각적 효과 표시
 * - 엔진 이펙트, 조종면 애니메이션 등
 * - RTOS 로직과 무관한 순수 시각화
 */

using UnityEngine;

namespace RTOScope.Runtime.Aircraft
{
    /// <summary>
    /// 항공기 시각화 컴포넌트
    /// AircraftState를 읽어 시각적 피드백 제공
    /// </summary>
    public class AircraftView : MonoBehaviour
    {
        // =====================================================================
        // [Unity 직렬화 규칙 설명]
        // 
        // Unity의 [Header], [SerializeField], [Tooltip] 등의 속성(Attribute)은
        // 오직 "필드(field)"에만 적용할 수 있습니다.
        // 
        // 프로퍼티(property)는 get/set 접근자를 가진 멤버로,
        // Unity Inspector에서 직렬화되지 않으므로 이러한 속성을 사용할 수 없습니다.
        // 
        // [잘못된 예]
        // [Header("References")]
        // public AircraftState State { get; set; }  // ❌ 컴파일 오류!
        // 
        // [올바른 예]
        // [Header("References")]
        // [SerializeField] private AircraftState _state;  // ✓ 필드에 적용
        // public AircraftState State => _state;           // ✓ 읽기 전용 접근자
        // =====================================================================

        // AircraftState는 런타임에 RTOSRunner에서 주입되므로
        // Inspector 직렬화가 필요 없음 → 단순 프로퍼티로 유지
        // (Unity 속성 제거)
        /// <summary>
        /// 항공기 상태 데이터 (RTOSRunner에서 주입됨)
        /// </summary>
        public AircraftState State { get; set; }

        [Header("Visual Effects")]
        [SerializeField] private Transform[] _ailerons;     // 에일러론 (롤 제어)
        [SerializeField] private Transform _elevator;        // 승강타 (피치 제어)
        [SerializeField] private Transform _rudder;          // 방향타 (요 제어)
        [SerializeField] private ParticleSystem _engineTrail;

        [Header("Settings")]
        [SerializeField] private float _controlSurfaceMaxAngle = 30f;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================
        
        private void Update()
        {
            if (State == null) return;

            // TODO: 시각화 로직 구현
            UpdateControlSurfaces();
            UpdateEngineEffects();
        }

        // =====================================================================
        // 시각화 메서드
        // =====================================================================

        private void UpdateControlSurfaces()
        {
            // TODO: 조종면 애니메이션 구현
            // - State.PitchCommand에 따라 승강타 회전
            // - State.RollCommand에 따라 에일러론 회전
            // - State.YawCommand에 따라 방향타 회전
        }

        private void UpdateEngineEffects()
        {
            // TODO: 엔진 이펙트 구현
            // - State.ThrottleCommand에 따라 파티클 강도 조절
        }

        /// <summary>
        /// HUD 정보 표시
        /// </summary>
        public void DrawHUD()
        {
            // TODO: UI 시스템과 연동
        }
    }
}
