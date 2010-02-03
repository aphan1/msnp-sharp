#region Copyright (c) 2002-2010, Bas Geertsema, Xih Solutions (http://www.xihsolutions.net), Thiago.Sayao, Pang Wu, Ethem Evlice
/*
Copyright (c) 2002-2010, Bas Geertsema, Xih Solutions
(http://www.xihsolutions.net), Thiago.Sayao, Pang Wu, Ethem Evlice.
All rights reserved. http://code.google.com/p/msnp-sharp/

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.
* Neither the names of Bas Geertsema or Xih Solutions nor the names of its
  contributors may be used to endorse or promote products derived from this
  software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 'AS IS'
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE. 
*/
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace MSNPSharp
{
    using MSNPSharp.DataTransfer;
    using MSNPSharp.Core;

    /// <summary>
    /// The queue object for scheduler.
    /// </summary>
    internal class SchedulerQueueObject
    {
        IMessageProcessor messageProcessor = null;
        NetworkMessage message = null;
        Guid messengerId = Guid.Empty;
        DateTime createTime = DateTime.MinValue;

        /// <summary>
        /// The time this object created.
        /// </summary>
        public DateTime CreateTime
        {
            get { return createTime; }
        }

        /// <summary>
        /// The <see cref="IMessageProcessor">MessageProcessor</see> who send the message.
        /// </summary>
        public IMessageProcessor MessageProcessor
        {
            get { return messageProcessor; }
        }

        /// <summary>
        /// The message need to be sent.
        /// </summary>
        public NetworkMessage Message
        {
            get { return message; }
        }

        /// <summary>
        /// The <see cref="Messenger"/> who schedule this message.
        /// </summary>
        public Guid MessengerId
        {
            get { return messengerId; }
        }

        private SchedulerQueueObject()
        {
        }

        public SchedulerQueueObject(IMessageProcessor processor, NetworkMessage message, Guid owner)
        {
            messageProcessor = processor;
            this.message = message;
            messengerId = owner;
            createTime = DateTime.Now;
        }
    }

    internal interface IScheduler
    {
        int DelayTime
        {
            get;
        }

        void Enqueue(IMessageProcessor processor, NetworkMessage message, Guid ownerId);
        Guid Register(Messenger messenger);
        bool UnRegister(Guid id);
    }

    /// <summary>
    /// Delay sending the p2p invitation messages, avoid p2p data transfer request a new conversation.
    /// </summary>
    internal class Scheduler : IScheduler
    {
        private object syncObject = new object();
        private int delayTime = 5000; //In ms.
        private Thread timerThread = null;
        private Queue<SchedulerQueueObject> messageQueue = new Queue<SchedulerQueueObject>();
        private Dictionary<Guid, Messenger> messengerList = new Dictionary<Guid, Messenger>(0);

        public int DelayTime
        {
            get { return delayTime; }
        }

        #region Private method

        protected virtual void EnqueueMessage(IMessageProcessor processor, NetworkMessage message, Guid ownerId)
        {
            if (processor == null || message == null)
                return;

            lock (syncObject)
            {
                if (messengerList.ContainsKey(ownerId))
                {
                    messageQueue.Enqueue(new SchedulerQueueObject(processor, message, ownerId));
                }
            }
        }

        protected virtual void OnTimerCallback(object state)
        {
            while (true)
            {
                DateTime currentTime = DateTime.Now;
                TimeSpan span = new TimeSpan(0, 0, 0, 0, DelayTime);

                lock (syncObject)
                {
                    while (messageQueue.Count > 0 && currentTime - messageQueue.Peek().CreateTime >= span)
                    {
                        SchedulerQueueObject item = messageQueue.Dequeue();
                        if (messengerList.ContainsKey(item.MessengerId) && messengerList[item.MessengerId].Connected)
                        {
                            item.MessageProcessor.SendMessage(item.Message);
                        }
                    }


                    if (messageQueue.Count == 0)
                    {
                        return;
                    }
                }

                Thread.Sleep(span);
            }
        }

        #endregion

        #region Public Method

        public Scheduler(int delay)
        {
            delayTime = delay;
            timerThread = new Thread(new ParameterizedThreadStart(OnTimerCallback));
        }

        /// <summary>
        /// Add the message to sending queue.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="message"></param>
        /// <param name="ownerId"></param>
        public virtual void Enqueue(IMessageProcessor processor, NetworkMessage message, Guid ownerId)
        {
            lock (syncObject)
            {
                if (messengerList.ContainsKey(ownerId))
                {
                    if (timerThread.ThreadState == System.Threading.ThreadState.Unstarted ||
                        timerThread.ThreadState == System.Threading.ThreadState.Stopped)
                    {
                        timerThread = new Thread(new ParameterizedThreadStart(OnTimerCallback));
                        timerThread.Start(DelayTime);
                    }
                }
                else
                {
                    return;
                }
            }

            EnqueueMessage(processor, message, ownerId);
        }

        public virtual Guid Register(Messenger messenger)
        {
            lock (syncObject)
            {
                foreach (Guid guid in messengerList.Keys)
                {
                    if (object.ReferenceEquals(messenger, messengerList[guid]))
                    {
                        return guid;
                    }
                }

                Guid newId = Guid.NewGuid();
                while (messengerList.ContainsKey(newId))
                {
                    newId = Guid.NewGuid();
                }

                messengerList[newId] = messenger;
                return newId;
            }
        }

        public virtual bool UnRegister(Guid id)
        {
            lock (syncObject)
            {
                return messengerList.Remove(id);
            }
        }

        #endregion
    }

    internal static class Schedulers
    {
        private static Scheduler p2pInvitationScheduler = new Scheduler(5000);
        private static Scheduler sbRequestScheduler = new Scheduler(1000);

        internal static Scheduler SwitchBoardRequestScheduler
        {
            get 
            { 
                return Schedulers.sbRequestScheduler; 
            }
        }

        internal static Scheduler P2PInvitationScheduler
        {
            get 
            { 
                return Schedulers.p2pInvitationScheduler; 
            }
        }
    }
}
