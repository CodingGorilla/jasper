﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jasper.Messaging.Runtime;
using Jasper.Messaging.Transports.Util;

namespace Jasper.Messaging.Tracking
{
    public class MessageHistory
    {
        private readonly IList<MessageTrack> _completed = new List<MessageTrack>();

        private readonly IList<Exception> _exceptions = new List<Exception>();
        private readonly object _lock = new object();
        private readonly Dictionary<string, MessageTrack> _outstanding = new Dictionary<string, MessageTrack>();

        private readonly IList<TaskCompletionSource<MessageTrack[]>> _waiters =
            new List<TaskCompletionSource<MessageTrack[]>>();

        public Task<MessageTrack[]> Watch(Action action)
        {
            var waiter = new TaskCompletionSource<MessageTrack[]>();

            lock (_lock)
            {
                _waiters.Clear();

                _completed.Clear();
                _outstanding.Clear();

                _exceptions.Clear();

                _waiters.Add(waiter);
            }

            action();

            return waiter.Task;
        }


        public async Task<MessageTrack[]> WatchAsync(Func<Task> func, int timeoutInMilliseconds = 5000)
        {
            var waiter = new TaskCompletionSource<MessageTrack[]>();


            lock (_lock)
            {
                _waiters.Clear();

                _completed.Clear();
                _outstanding.Clear();

                _exceptions.Clear();

                _waiters.Add(waiter);
            }

            await func();

            return await waiter.Task.TimeoutAfter(timeoutInMilliseconds);
        }

        public void Complete(Envelope envelope, string activity, Exception ex = null)
        {
            var key = MessageTrack.ToKey(envelope, activity);
            var messageType = envelope.Message?.GetType();
            lock (_lock)
            {
                if (_outstanding.ContainsKey(key))
                {
                    var track = _outstanding[key];
                    _outstanding.Remove(key);

                    track.Finish(envelope, ex);

                    _completed.Add(track);

                    processCompletion();
                }
            }
        }

        public void Start(Envelope envelope, string activity)
        {
            var track = new MessageTrack(envelope, activity);
            lock (_lock)
            {
                if (_outstanding.ContainsKey(track.Key))
                    _outstanding[track.Key] = track;
                else
                    _outstanding.Add(track.Key, track);
            }
        }

        private void processCompletion()
        {
            if (_outstanding.Count == 0 && _completed.Count > 0)
            {
                var tracks = _completed.Distinct().ToArray();

                foreach (var waiter in _waiters) waiter.SetResult(tracks);

                _waiters.Clear();
            }
        }

        public void LogException(Exception exception)
        {
            _exceptions.Add(exception);
        }

        public void AssertNoExceptions()
        {
            if (_exceptions.Any()) throw new AggregateException(_exceptions);
        }
    }
}
