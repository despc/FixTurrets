﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.WorldEnvironment;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Network;

namespace SentisOptimisationsPlugin
{
    public class SendReplicablesAsync
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Queue<VoxelsPatch.SendToClientWrapper> _queue = new Queue<VoxelsPatch.SendToClientWrapper>();
       
        public CancellationTokenSource CancellationTokenSource { get; set; }

        public void OnLoaded()
        {
            CancellationTokenSource = new CancellationTokenSource();
            Task.Run(SendToClient);
        }

        public void OnUnloading()
        {
            CancellationTokenSource.Cancel();
        }

        public void SendToClient()
        {
            try
            {
                Log.Info("Send to client loop started");
                while (!CancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        Thread.Sleep(1);
                        if (_queue.Count == 0)
                        {
                            continue;
                        }
                        var dequeue = _queue.Dequeue();
                        dequeue.DoSendToClient();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Send to client loop Error", e);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Send to client loop Error", e);
            }
        }        

    }
}