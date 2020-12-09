﻿using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Commons.Time;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using ZooKeeperNetExClient = org.apache.zookeeper.ZooKeeper;

namespace Vostok.ZooKeeper.Testing
{
    [PublicAPI]
    public static class ZooKeeperClientTestsHelper
    {
        public static async Task KillSession(long sessionId, byte[] sessionPassword, IObservable<ConnectionState> onConnectionStateChanged, string connectionString, TimeSpan timeout)
        {
            var observer = new WaitStateObserver(ConnectionState.Expired);
            onConnectionStateChanged.Subscribe(observer);

            var budged = TimeBudget.StartNew(timeout);

            await ZooKeeperNetExClient.Using(
                    connectionString,
                    5000,
                    null,
                    sessionId,
                    sessionPassword,
                    async zk =>
                    {
                        while (!budged.HasExpired)
                        {
                            if (zk.getState().Equals(ZooKeeperNetExClient.States.CONNECTED))
                            {
                                break;
                            }

                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    })
               .ConfigureAwait(false);

            if (await observer.Signal.Task.WaitAsync(budged.Remaining).ConfigureAwait(false))
                return;

            throw new TimeoutException($"Expected to kill session within {timeout}, but failed to do so.");
        }

        private class WaitStateObserver : IObserver<ConnectionState>
        {
            public readonly TaskCompletionSource<bool> Signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ConnectionState desiredState;

            public WaitStateObserver(ConnectionState desiredState)
            {
                this.desiredState = desiredState;
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(ConnectionState value)
            {
                if (value == desiredState)
                    Signal.SetResult(true);
            }
        }
    }
}