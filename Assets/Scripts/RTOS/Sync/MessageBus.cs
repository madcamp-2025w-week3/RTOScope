/*
 * MessageBus.cs - RTOS 메시지 버스
 * 
 * [역할] 태스크 간 비동기 메시지 전달, 이벤트 기반 통신
 * [위치] RTOS Layer > Sync (Unity API 사용 금지)
 * 
 * [미구현] 메시지 우선순위, 브로드캐스트
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Sync
{
    /// <summary>
    /// 메시지 타입
    /// </summary>
    public enum MessageType
    {
        Command,    // 명령
        Data,       // 데이터 전달
        Event,      // 이벤트 알림
        Ack         // 확인 응답
    }

    /// <summary>
    /// RTOS 메시지
    /// </summary>
    public class Message
    {
        public int Id { get; set; }
        public MessageType Type { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }     // -1이면 브로드캐스트
        public object Payload { get; set; }
        public DateTime Timestamp { get; set; }

        public Message(MessageType type, int senderId, int receiverId, object payload)
        {
            Type = type;
            SenderId = senderId;
            ReceiverId = receiverId;
            Payload = payload;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// RTOS 메시지 버스 - 태스크 간 통신
    /// </summary>
    public class MessageBus
    {
        private readonly Dictionary<int, Queue<Message>> _mailboxes;
        private int _nextMessageId = 0;
        private readonly object _lock = new object();

        public MessageBus()
        {
            _mailboxes = new Dictionary<int, Queue<Message>>();
        }

        /// <summary>
        /// 태스크의 메일박스 등록
        /// </summary>
        public void RegisterMailbox(int taskId)
        {
            lock (_lock)
            {
                if (!_mailboxes.ContainsKey(taskId))
                    _mailboxes[taskId] = new Queue<Message>();
            }
        }

        /// <summary>
        /// 메시지 전송
        /// </summary>
        public void Send(Message message)
        {
            if (message == null) return;
            
            lock (_lock)
            {
                message.Id = _nextMessageId++;
                
                if (message.ReceiverId == -1)
                {
                    // TODO: 브로드캐스트 구현
                    foreach (var mailbox in _mailboxes.Values)
                        mailbox.Enqueue(message);
                }
                else if (_mailboxes.TryGetValue(message.ReceiverId, out var mailbox))
                {
                    mailbox.Enqueue(message);
                }
            }
        }

        /// <summary>
        /// 메시지 수신
        /// </summary>
        public Message Receive(int taskId)
        {
            lock (_lock)
            {
                if (_mailboxes.TryGetValue(taskId, out var mailbox) && mailbox.Count > 0)
                    return mailbox.Dequeue();
                return null;
            }
        }

        /// <summary>
        /// 대기 중인 메시지 개수
        /// </summary>
        public int GetPendingCount(int taskId)
        {
            lock (_lock)
            {
                return _mailboxes.TryGetValue(taskId, out var mailbox) ? mailbox.Count : 0;
            }
        }
    }
}
