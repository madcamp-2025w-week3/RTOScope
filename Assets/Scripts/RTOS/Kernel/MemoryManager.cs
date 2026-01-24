/*
 * MemoryManager.cs - RTOS 메모리 관리자
 * 
 * [역할] 메모리 풀 관리, 동적 메모리 할당 시뮬레이션
 * [위치] RTOS Layer > Kernel (Unity API 사용 금지)
 * 
 * [미구현] 메모리 단편화 처리, 가비지 컬렉션 시뮬레이션
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// 메모리 블록 정보
    /// </summary>
    public class MemoryBlock
    {
        public int Id { get; set; }
        public int Size { get; set; }
        public bool IsAllocated { get; set; }
        public int OwnerId { get; set; }  // 태스크 ID
    }

    /// <summary>
    /// RTOS 메모리 관리자 - 정적 메모리 풀 시뮬레이션
    /// </summary>
    public class MemoryManager
    {
        private readonly int _totalMemory;
        private readonly List<MemoryBlock> _blocks = new List<MemoryBlock>();
        private int _nextBlockId = 0;
        private int _usedMemory = 0;

        public int TotalMemory => _totalMemory;
        public int UsedMemory => _usedMemory;
        public int FreeMemory => _totalMemory - _usedMemory;
        public float UtilizationPercent => (float)_usedMemory / _totalMemory * 100f;

        public MemoryManager(int totalMemory = 1024 * 1024)  // 기본 1MB
        {
            _totalMemory = totalMemory;
        }

        // TODO: 메모리 할당 알고리즘 구현 (First Fit, Best Fit 등)
        public MemoryBlock Allocate(int size, int ownerId)
        {
            if (_usedMemory + size > _totalMemory)
                return null;  // 메모리 부족

            var block = new MemoryBlock
            {
                Id = _nextBlockId++,
                Size = size,
                IsAllocated = true,
                OwnerId = ownerId
            };
            _blocks.Add(block);
            _usedMemory += size;
            return block;
        }

        public bool Free(int blockId)
        {
            var block = _blocks.Find(b => b.Id == blockId);
            if (block == null || !block.IsAllocated)
                return false;

            block.IsAllocated = false;
            _usedMemory -= block.Size;
            return true;
        }

        // TODO: 메모리 단편화 분석
        public float GetFragmentationRatio() => 0f;

        public void Reset()
        {
            _blocks.Clear();
            _usedMemory = 0;
        }
    }
}
