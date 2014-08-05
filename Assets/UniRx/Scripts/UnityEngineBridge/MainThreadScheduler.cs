﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace UniRx
{
    public static partial class Scheduler
    {
        static IScheduler mainThread;

        public static IScheduler MainThread
        {
            get
            {
                return mainThread ?? (mainThread = new MainThreadScheduler());
            }
        }

        class MainThreadScheduler : IScheduler
        {
            public MainThreadScheduler()
            {
                MainThreadDispatcher.Initialize();
            }

            // delay action is run in StartCoroutine
            // Okay to action run synchronous
            IEnumerator DelayAction(TimeSpan dueTime, Action action, ICancelable cancellation)
            {
                if (dueTime == TimeSpan.Zero)
                {
                    yield return null; // not immediately, run next frame
                    MainThreadDispatcher.Send(action);
                }
                else if (dueTime.TotalMilliseconds % 1000 == 0)
                {
                    yield return new WaitForSeconds((float)dueTime.TotalSeconds);
                    MainThreadDispatcher.Send(action);
                }
                else
                {
                    var startTime = Time.time;
                    var dt = (float)dueTime.TotalSeconds;
                    while (!cancellation.IsDisposed)
                    {
                        yield return null;
                        var elapsed = Time.time - startTime;
                        if (elapsed >= dt)
                        {
                            MainThreadDispatcher.Send(action);
                            break;
                        }
                    }
                }
            }

            public DateTimeOffset Now
            {
                get { return Scheduler.Now; }
            }

            public IDisposable Schedule(Action action)
            {
                var d = new BooleanDisposable();
                MainThreadDispatcher.Post(() =>
                {
                    if (!d.IsDisposed)
                    {
                        action();
                    }
                });
                return d;
            }

            public IDisposable Schedule(DateTimeOffset dueTime, Action action)
            {
                return Schedule(dueTime - Now, action);
            }

            public IDisposable Schedule(TimeSpan dueTime, Action action)
            {
                var d = new BooleanDisposable();
                var time = Normalize(dueTime);

                MainThreadDispatcher.SendStartCoroutine(DelayAction(time, () =>
                {
                    if (!d.IsDisposed)
                    {
                        action();
                    }
                }, d));

                return d;
            }
        }
    }
}