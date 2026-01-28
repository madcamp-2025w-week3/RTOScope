/*
 * ============================================================================
 * ReadyList.cs - RTOS Ready List (FreeRTOS 스타일)
 * ============================================================================
 *
 * [모듈 역할]
 * 실행 대기 중인 태스크를 우선순위별로 관리하는 Ready List
 *
 * [아키텍처 위치]
 * RTOS Layer > Kernel > ReadyList
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 *
 * [설계 철학]
 * - FreeRTOS의 pxReadyTasksLists 구조를 모방
 * - 우선순위별 연결 리스트 배열
 * - 비트맵으로 빠른 최고 우선순위 탐색
 * - 동일 우선순위 내 Round-Robin 지원
 *
 * [RTOS 개념]
 * - Ready List: CPU 할당을 기다리는 태스크들의 집합
 * - 스케줄러는 항상 가장 높은 우선순위의 Ready 태스크를 실행
 * - 동일 우선순위에서는 FIFO(또는 Round-Robin) 순서
 * ============================================================================
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// RTOS Ready List - 우선순위별 태스크 대기 목록
    /// FreeRTOS의 pxReadyTasksLists 구조를 모방
    /// </summary>
    public class ReadyList
    {
        // =====================================================================
        // 상수 및 필드
        // =====================================================================

        /// <summary>최대 우선순위 레벨 수 (0 = 최고, 255 = 최저/Idle)</summary>
        public const int MAX_PRIORITY_LEVELS = 256;

        /// <summary>우선순위별 Ready 태스크 리스트 (연결 리스트 배열)</summary>
        private readonly LinkedList<TCB>[] _priorityLists;

        /// <summary>
        /// Ready 비트맵 (32비트 * 8 = 256개 우선순위)
        /// 비트가 1이면 해당 우선순위에 Ready 태스크가 있음
        /// </summary>
        private readonly uint[] _readyBitmap;

        /// <summary>현재 최고 우선순위 (캐시)</summary>
        private int _topPriority;

        /// <summary>총 Ready 태스크 수</summary>
        private int _count;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        /// <summary>Ready List에 있는 총 태스크 수</summary>
        public int Count => _count;

        /// <summary>Ready 태스크가 없는지 여부</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>현재 최고 우선순위 (태스크 없으면 -1)</summary>
        public int TopPriority => _topPriority;

        // =====================================================================
        // 생성자
        // =====================================================================

        public ReadyList()
        {
            _priorityLists = new LinkedList<TCB>[MAX_PRIORITY_LEVELS];
            _readyBitmap = new uint[MAX_PRIORITY_LEVELS / 32]; // 256 / 32 = 8

            // 모든 우선순위 리스트 초기화
            for (int i = 0; i < MAX_PRIORITY_LEVELS; i++)
            {
                _priorityLists[i] = new LinkedList<TCB>();
            }

            _topPriority = -1;
            _count = 0;
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>
        /// 태스크를 Ready List에 추가한다.
        /// 해당 우선순위 리스트의 끝에 추가 (FIFO 순서 유지)
        /// </summary>
        /// <param name="tcb">추가할 TCB</param>
        public void Add(TCB tcb)
        {
            if (tcb == null)
                throw new ArgumentNullException(nameof(tcb));

            int priority = (int)tcb.CurrentPriority;

            // 해당 우선순위 리스트 끝에 추가 (FIFO)
            _priorityLists[priority].AddLast(tcb);
            _count++;

            // 비트맵 업데이트
            SetBit(priority);

            // 최고 우선순위 갱신
            if (_topPriority < 0 || priority < _topPriority)
            {
                _topPriority = priority;
            }
        }

        /// <summary>
        /// 가장 높은 우선순위의 Ready 태스크를 제거하고 반환한다.
        /// 동일 우선순위 내에서는 FIFO (먼저 들어온 태스크 우선)
        /// </summary>
        /// <returns>가장 높은 우선순위의 TCB, 없으면 null</returns>
        public TCB RemoveHighest()
        {
            if (_count == 0)
                return null;

            var list = _priorityLists[_topPriority];
            if (list.Count == 0)
            {
                // 비정상 상태 - 리커버리
                RecalculateTopPriority();
                return null;
            }

            // 리스트의 첫 번째 태스크 제거 (FIFO)
            TCB tcb = list.First.Value;
            list.RemoveFirst();
            _count--;

            // 해당 우선순위 리스트가 비었으면 비트맵 클리어
            if (list.Count == 0)
            {
                ClearBit(_topPriority);
                RecalculateTopPriority();
            }

            return tcb;
        }

        /// <summary>
        /// 가장 높은 우선순위의 Ready 태스크를 확인만 한다 (제거 안함)
        /// </summary>
        /// <returns>가장 높은 우선순위의 TCB, 없으면 null</returns>
        public TCB PeekHighest()
        {
            if (_count == 0 || _topPriority < 0)
                return null;

            var list = _priorityLists[_topPriority];
            return list.Count > 0 ? list.First.Value : null;
        }

        /// <summary>
        /// 특정 태스크를 Ready List에서 제거한다.
        /// 태스크가 Blocked/Suspended 될 때 호출됨
        /// </summary>
        /// <param name="tcb">제거할 TCB</param>
        /// <returns>제거 성공 여부</returns>
        public bool Remove(TCB tcb)
        {
            if (tcb == null)
                return false;

            int priority = (int)tcb.CurrentPriority;
            var list = _priorityLists[priority];

            if (list.Remove(tcb))
            {
                _count--;

                // 해당 우선순위 리스트가 비었으면 비트맵 클리어
                if (list.Count == 0)
                {
                    ClearBit(priority);
                    if (priority == _topPriority)
                    {
                        RecalculateTopPriority();
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 태스크가 Ready List에 있는지 확인한다.
        /// </summary>
        public bool Contains(TCB tcb)
        {
            if (tcb == null)
                return false;

            int priority = (int)tcb.CurrentPriority;
            return _priorityLists[priority].Contains(tcb);
        }

        /// <summary>
        /// Ready List를 모두 비운다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < MAX_PRIORITY_LEVELS; i++)
            {
                _priorityLists[i].Clear();
            }

            for (int i = 0; i < _readyBitmap.Length; i++)
            {
                _readyBitmap[i] = 0;
            }

            _topPriority = -1;
            _count = 0;
        }

        /// <summary>
        /// 특정 우선순위 레벨의 태스크 수를 반환한다.
        /// </summary>
        public int GetCountAtPriority(int priority)
        {
            if (priority < 0 || priority >= MAX_PRIORITY_LEVELS)
                return 0;
            return _priorityLists[priority].Count;
        }

        /// <summary>
        /// Round-Robin: 현재 실행 중인 태스크를 같은 우선순위 리스트의 끝으로 이동
        /// 동일 우선순위 태스크들 간의 공정한 스케줄링을 위함
        /// </summary>
        /// <param name="tcb">이동할 TCB</param>
        public void MoveToEnd(TCB tcb)
        {
            if (tcb == null)
                return;

            int priority = (int)tcb.CurrentPriority;
            var list = _priorityLists[priority];

            if (list.Remove(tcb))
            {
                list.AddLast(tcb);
            }
        }

        /// <summary>
        /// 모든 Ready 상태 태스크를 리스트로 반환 (스케줄러 인터페이스용)
        /// </summary>
        /// <returns>Ready 태스크 목록</returns>
        public IReadOnlyList<TCB> GetAllReady()
        {
            var result = new List<TCB>(_count);
            
            // 우선순위 순서대로 모든 Ready 태스크 수집
            for (int i = 0; i < MAX_PRIORITY_LEVELS; i++)
            {
                foreach (var tcb in _priorityLists[i])
                {
                    result.Add(tcb);
                }
            }
            
            return result;
        }

        // =====================================================================
        // 디버그/모니터링용
        // =====================================================================

        /// <summary>
        /// Ready List 상태를 문자열로 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ReadyList] Count={_count}, TopPriority={_topPriority}");

            for (int i = 0; i < MAX_PRIORITY_LEVELS; i++)
            {
                if (_priorityLists[i].Count > 0)
                {
                    sb.Append($"  Pri[{i}]: ");
                    foreach (var tcb in _priorityLists[i])
                    {
                        sb.Append($"{tcb.Task.Name}, ");
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        // =====================================================================
        // 비공개 메서드 - 비트맵 연산
        // =====================================================================

        /// <summary>비트맵에서 해당 우선순위 비트 설정</summary>
        private void SetBit(int priority)
        {
            int index = priority / 32;
            int bit = priority % 32;
            _readyBitmap[index] |= (1u << bit);
        }

        /// <summary>비트맵에서 해당 우선순위 비트 해제</summary>
        private void ClearBit(int priority)
        {
            int index = priority / 32;
            int bit = priority % 32;
            _readyBitmap[index] &= ~(1u << bit);
        }

        /// <summary>비트맵에서 해당 우선순위에 태스크가 있는지 확인</summary>
        private bool IsBitSet(int priority)
        {
            int index = priority / 32;
            int bit = priority % 32;
            return (_readyBitmap[index] & (1u << bit)) != 0;
        }

        /// <summary>비트맵을 스캔하여 최고 우선순위 재계산</summary>
        private void RecalculateTopPriority()
        {
            // 가장 낮은 비트(높은 우선순위)부터 스캔
            for (int i = 0; i < _readyBitmap.Length; i++)
            {
                if (_readyBitmap[i] != 0)
                {
                    // Find first set bit (가장 낮은 번호의 1 비트)
                    int firstBit = FindFirstSetBit(_readyBitmap[i]);
                    _topPriority = i * 32 + firstBit;
                    return;
                }
            }
            _topPriority = -1; // Ready 태스크 없음
        }

        /// <summary>32비트 값에서 가장 낮은 1 비트의 위치 반환</summary>
        private int FindFirstSetBit(uint value)
        {
            // Software-based BSF (Bit Scan Forward)
            for (int i = 0; i < 32; i++)
            {
                if ((value & (1u << i)) != 0)
                    return i;
            }
            return -1;
        }
    }
}
