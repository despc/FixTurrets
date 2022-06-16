﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using SentisOptimisations;
using Torch.Managers.PatchManager;
using VRage.Game;

namespace SentisOptimisationsPlugin
{
    [PatchShim]
    public static class ExplosionsPatch
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Random Random = new Random();
        private static int m_updateCounter = 0;
        

        public static void Patch(PatchContext ctx)
        {
            var UpdateAfterSimulationParallel = typeof(MyFloatingObject).GetMethod
                (nameof(MyFloatingObject.UpdateAfterSimulationParallel), BindingFlags.Instance | BindingFlags.Public);

            ctx.GetPattern(UpdateAfterSimulationParallel).Prefixes.Add(
                typeof(ExplosionsPatch).GetMethod(nameof(UpdateAfterSimulationParallelPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            var CheckObjectInVoxel = typeof(MyFloatingObjects).GetMethod
                ("CheckObjectInVoxel", BindingFlags.Instance | BindingFlags.NonPublic);
            
            ctx.GetPattern(CheckObjectInVoxel).Prefixes.Add(
                typeof(ExplosionsPatch).GetMethod(nameof(CheckObjectInVoxelPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            
            
            var RegisterFloatingObject = typeof(MyFloatingObjects).GetMethod
                ("RegisterFloatingObject", BindingFlags.Static | BindingFlags.NonPublic);
            
            ctx.GetPattern(RegisterFloatingObject).Prefixes.Add(
                typeof(ExplosionsPatch).GetMethod(nameof(RegisterFloatingObjectPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            
            var ReduceFloatingObjects = typeof(MyFloatingObjects).GetMethod
                (nameof(MyFloatingObjects.ReduceFloatingObjects), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            
            ctx.GetPattern(ReduceFloatingObjects).Prefixes.Add(
                typeof(ExplosionsPatch).GetMethod(nameof(ReduceFloatingObjectsPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }

        private static bool ReduceFloatingObjectsPatched()
        {
            try
            {
                SortedSet<MyFloatingObject> m_floatingOres =
                    (SortedSet<MyFloatingObject>) ReflectionUtils.GetPrivateStaticField(typeof(MyFloatingObjects), "m_floatingOres");
                SortedSet<MyFloatingObject> m_floatingItems =
                    (SortedSet<MyFloatingObject>) ReflectionUtils.GetPrivateStaticField(typeof(MyFloatingObjects), "m_floatingItems");
                int num1 = m_floatingOres.Count + m_floatingItems.Count;
                int num2 = Math.Max((int) MySession.Static.MaxFloatingObjects / 5, 4);
                for (; num1 > (int) MySession.Static.MaxFloatingObjects; --num1)
                {
                    SortedSet<MyFloatingObject> source = m_floatingOres.Count > num2 || m_floatingItems.Count == 0 ? m_floatingOres : m_floatingItems;
                    if (source.Count > 0)
                    {
                        MyFloatingObject myFloatingObject = source.Last<MyFloatingObject>();
                        source.Remove(myFloatingObject);
                        MyFloatingObjects.RemoveFloatingObject(myFloatingObject);
                    }
                }
            }
            catch (Exception e)
            {
                //
            }
            return false;
        }

        private static bool RegisterFloatingObjectPatched(MyFloatingObject obj)
        {
            try
            {
                if (obj == null || obj.WasRemovedFromWorld)
                    return false;
                obj.CreationTime = Stopwatch.GetTimestamp();
                if (obj.VoxelMaterial != null)
                    ((SortedSet<MyFloatingObject>)ReflectionUtils.GetPrivateStaticField(typeof(MyFloatingObjects), "m_floatingOres")).Add(obj);
                else
                    ((SortedSet<MyFloatingObject>)ReflectionUtils.GetPrivateStaticField(typeof(MyFloatingObjects), "m_floatingItems")).Add(obj);
                ReflectionUtils.InvokeStaticMethod(typeof(MyFloatingObjects), "AddToSynchronization", new object[] {obj});
            }
            catch (Exception e)
            {

            }
            return false;
        }

        private static bool CheckObjectInVoxelPatched()
        {
            return false;
        }

        private static bool UpdateAfterSimulationParallelPatched(MyFloatingObject __instance)
        {
            try
            {
                var acceleration = __instance.Physics.LinearAcceleration;
                if (acceleration.X > SentisOptimisationsPlugin.Config.AccelerationToDamage
                    || acceleration.Y > SentisOptimisationsPlugin.Config.AccelerationToDamage
                    || acceleration.Z > SentisOptimisationsPlugin.Config.AccelerationToDamage)
                {
                    __instance.DoDamage(999, MyDamageType.Explosion, true, 0);
                    return false;
                }
                
            }
            catch (Exception e)
            {
                //
            }

            return true;
        }

    }
}