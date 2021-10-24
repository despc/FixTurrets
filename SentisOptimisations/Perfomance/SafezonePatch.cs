﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Havok;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using SentisOptimisations;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRage.Groups;
using VRage.ModAPI;

namespace SentisOptimisationsPlugin
{
    [PatchShim]
    public static class SafezonePatch
    {
        public static Dictionary<long, long> entitiesInSZ = new Dictionary<long, long>();

        public static void Patch(PatchContext ctx)
        {
            var MethodPhantom_Leave = typeof(MySafeZone).GetMethod
                ("phantom_Leave", BindingFlags.Instance | BindingFlags.NonPublic);

            ctx.GetPattern(MethodPhantom_Leave).Prefixes.Add(
                typeof(SafezonePatch).GetMethod(nameof(MethodPhantom_LeavePatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            // var MySafeZoneUpdateBeforeSimulation = typeof(MySafeZone).GetMethod
            //     (nameof(MySafeZone.UpdateBeforeSimulation), BindingFlags.Instance | BindingFlags.Public);
            //
            // ctx.GetPattern(MySafeZoneUpdateBeforeSimulation).Prefixes.Add(
            //     typeof(SafezonePatch).GetMethod(nameof(MySafeZoneUpdateBeforeSimulationPatched),
            //         BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            
            var MySafeZoneIsSafe = typeof(MySafeZone).GetMethod
                ("IsSafe", BindingFlags.Instance | BindingFlags.NonPublic);

            ctx.GetPattern(MySafeZoneIsSafe).Prefixes.Add(
                typeof(SafezonePatch).GetMethod(nameof(MySafeZoneIsSafePatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            
            enumStringMapping["NOT_SAFE"] = SubgridCheckResult.NOT_SAFE;
            enumStringMapping["NEED_EXTRA_CHECK"] = SubgridCheckResult.NEED_EXTRA_CHECK;
            enumStringMapping["SAFE"] = SubgridCheckResult.SAFE;
            enumStringMapping["ADMIN"] = SubgridCheckResult.ADMIN;
        }


        private static bool MethodPhantom_LeavePatched(MySafeZone __instance, HkPhantomCallbackShape sender,
            HkRigidBody body)
        {
            try
            {
                IMyEntity entity = body.GetEntity(0U);
                if (entity == null)
                    return false;
                var stopwatch = Stopwatch.StartNew();
                ReflectionUtils.InvokeInstanceMethod(__instance.GetType(), __instance, "RemoveEntityPhantom",
                    new object[] {body, entity});
                stopwatch.Stop();
                var stopwatchElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                if (stopwatchElapsedMilliseconds < 4)
                {
                    return false;
                }

                if (entitiesInSZ.ContainsKey(entity.EntityId))
                {
                    entitiesInSZ[entity.EntityId] = entitiesInSZ[entity.EntityId] + stopwatchElapsedMilliseconds;
                }
                else
                {
                    entitiesInSZ[entity.EntityId] = stopwatchElapsedMilliseconds;
                }
            }
            catch (Exception e)
            {
                SentisOptimisationsPlugin.Log.Warn("MethodPhantom_LeavePatched Exception ", e);
            }

            return false;
        }

        private static bool MySafeZoneUpdateBeforeSimulationPatched(MySafeZone __instance)
        {
            try
            {
                if ((ulong) __instance.EntityId % 10 != MySandboxGame.Static.SimulationFrameCounter % 10)
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                SentisOptimisationsPlugin.Log.Warn("MySafeZoneUpdateBeforeSimulationPatched Exception ", e);
            }

            return true;
        }

        private static bool MySafeZoneIsSafePatched(MySafeZone __instance, MyEntity entity, ref bool __result)
        {
            try
            {
                MyFloatingObject myFloatingObject = entity as MyFloatingObject;
                MyInventoryBagEntity inventoryBagEntity = entity as MyInventoryBagEntity;
                if (myFloatingObject != null || inventoryBagEntity != null)
                    __result = __instance.Entities.Contains(entity.EntityId)
                        ? __instance.AccessTypeFloatingObjects == MySafeZoneAccess.Whitelist
                        : (uint) __instance.AccessTypeFloatingObjects > 0U;
                MyEntity topMostParent = entity.GetTopMostParent((System.Type) null);
                MyIDModule component;
                if (topMostParent is IMyComponentOwner<MyIDModule> myComponentOwner &&
                    myComponentOwner.GetComponent(out component))
                {
                    ulong steamId = MySession.Static.Players.TryGetSteamId(component.Owner);
                    if (steamId != 0UL && MySafeZone.CheckAdminIgnoreSafezones(steamId))
                        __result = true;
                    if (__instance.AccessTypePlayers == MySafeZoneAccess.Whitelist)
                    {
                        if (__instance.Players.Contains(component.Owner))
                            __result = true;
                    }
                    else if (__instance.Players.Contains(component.Owner))
                        __result = false;

                    if (MySession.Static.Factions.TryGetPlayerFaction(component.Owner) is MyFaction playerFaction)
                    {
                        if (__instance.AccessTypeFactions == MySafeZoneAccess.Whitelist)
                        {
                            if (__instance.Factions.Contains(playerFaction))
                                __result = true;
                        }
                        else if (__instance.Factions.Contains(playerFaction))
                            __result = false;
                    }

                    __result = __instance.AccessTypePlayers == MySafeZoneAccess.Blacklist;
                }

                if (topMostParent is MyCubeGrid nodeInGroup)
                {
                    MyGroupsBase<MyCubeGrid> groups = MyCubeGridGroups.Static.GetGroups(GridLinkTypeEnum.Mechanical);
                   SubgridCheckResult subgridCheckResult1 = SubgridCheckResult.NOT_SAFE;
                    
                    foreach (MyCubeGrid groupNode in groups.GetGroupNodes(nodeInGroup))
                    {
                        object result = ReflectionUtils.InvokeInstanceMethod(typeof(MySafeZone), __instance, "IsSubGridSafe", new []{groupNode});
                        SubgridCheckResult subgridCheckResult2 = MapToResult(result);
                        //MySafeZone.SubgridCheckResult subgridCheckResult2 = __instance.IsSubGridSafe(groupNode);
                        switch (subgridCheckResult2)
                        {
                            case SubgridCheckResult.NOT_SAFE:
                                if (subgridCheckResult1 != SubgridCheckResult.ADMIN)
                                {
                                    subgridCheckResult1 = SubgridCheckResult.NOT_SAFE;
                                    continue;
                                }

                                continue;
                            case SubgridCheckResult.NEED_EXTRA_CHECK:
                                if (subgridCheckResult2 > subgridCheckResult1)
                                {
                                    subgridCheckResult1 = subgridCheckResult2;
                                }
                                continue;
                            case SubgridCheckResult.SAFE:
                            case SubgridCheckResult.ADMIN:
                                __result = true;
                                return false;
                            default:
                                continue;
                        }
                    }

                    __result = subgridCheckResult1 >= SubgridCheckResult.SAFE;
                }

                switch (entity)
                {
                    case MyAmmoBase _:
                    case MyMeteor _:
                        if ((__instance.AllowedActions & MySafeZoneAction.Shooting) == (MySafeZoneAction) 0)
                            __result = false;
                        break;
                }

                __result = true;
            }
            catch (Exception e)
            {
                SentisOptimisationsPlugin.Log.Warn("MySafeZoneIsSafePatched Exception ", e);
            }

            return false;
        }

        private static SubgridCheckResult MapToResult(object result)
        {
            SubgridCheckResult response;
            if (enumMapping.TryGetValue(result, out response))
            {
                return response;
            }

            response = enumStringMapping[result.ToString()];
            enumMapping[result] = response;
            return response;
        }

        private enum SubgridCheckResult
        {
            NOT_SAFE,
            NEED_EXTRA_CHECK,
            SAFE,
            ADMIN
        }
        private  static Dictionary<string, SubgridCheckResult> enumStringMapping = new Dictionary<string, SubgridCheckResult>();
        private  static Dictionary<object, SubgridCheckResult> enumMapping = new Dictionary<object, SubgridCheckResult>();
    }
}