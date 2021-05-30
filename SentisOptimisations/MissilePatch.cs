﻿using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Entity.EntityComponents.Interfaces;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.Models;
using VRage.Groups;
using VRage.Utils;
using VRageMath;
using Game = Sandbox.Engine.Platform.Game;

namespace SentisOptimisationsPlugin
{
    [PatchShim]
    public static class MissilePatch
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            var UpdateBeforeSimulationMethod = typeof(MyMissile).GetMethod(
                nameof(MyMissile.UpdateBeforeSimulation), BindingFlags.Instance | BindingFlags.Public);
            ctx.GetPattern(UpdateBeforeSimulationMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(UpdateBeforeSimulationPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            var ExecuteExplosionMethod = typeof(MyMissile).GetMethod(
                "ExecuteExplosion", BindingFlags.Instance | BindingFlags.NonPublic);
            ctx.GetPattern(ExecuteExplosionMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(ExecuteExplosionMethodPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            var DoDamageInternalMethod = typeof(MySlimBlock).GetMethod(
                "DoDamageInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            ctx.GetPattern(DoDamageInternalMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(DoDamageInternalPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            var ApplyAccumulatedDamageMethod = typeof(MySlimBlock).GetMethod(
                "ApplyAccumulatedDamage", BindingFlags.Instance | BindingFlags.Public);
            ctx.GetPattern(ApplyAccumulatedDamageMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(ApplyAccumulatedDamagePatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));


            var MyComponentStackApplyDamageMethod = typeof(MyComponentStack).GetMethod(
                "ApplyDamage", BindingFlags.Instance | BindingFlags.Public);
            ctx.GetPattern(MyComponentStackApplyDamageMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(MyComponentStackApplyDamageMethodPatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));


            var MoveMissileMethod = typeof(MyMissiles).GetMethod(
                "OnMissileMoved", BindingFlags.Static | BindingFlags.Public);
            ctx.GetPattern(MoveMissileMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(MoveMissilePatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));


            var registerMissileMethod = typeof(MyMissiles).GetMethod(
                "RegisterMissile", BindingFlags.Static | BindingFlags.NonPublic);

            ctx.GetPattern(registerMissileMethod).Prefixes.Add(
                typeof(MissilePatch).GetMethod(nameof(RegisterMissilePatched),
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }


        private static bool UpdateBeforeSimulationPatched(MyMissile __instance)
        {
            try
            {
                if ((bool) GetInstanceField(typeof(MyMissile), __instance, "m_shouldExplode"))
                {
                    Log.Error("ExecuteExplosion ");
                    InvokeInstanceMethod(typeof(MyMissile), __instance, "ExecuteExplosion", new Object[0]);
                }
                else
                {
                    IMyGameLogicComponent myGameLogicComponent =
                        (IMyGameLogicComponent) GetInstanceField(typeof(MyEntity), __instance, "m_gameLogic");
                    myGameLogicComponent.UpdateBeforeSimulation(true);

                    var mPhysicsEnabled = (bool) GetInstanceField(typeof(MyMissile), __instance, "m_physicsEnabled");
                    if (mPhysicsEnabled)
                    {
                        SetInstanceField(typeof(MyMissile), __instance, "m_linearVelocity",
                            __instance.Physics.LinearVelocity);
                        __instance.Physics.AngularVelocity = Vector3.Zero;
                    }

                    MyMissileAmmoDefinition mMissileAmmoDefinition =
                        (MyMissileAmmoDefinition) GetInstanceField(typeof(MyMissile), __instance,
                            "m_missileAmmoDefinition");
                    Vector3 currentLinearVelocity =
                        (Vector3) GetInstanceField(typeof(MyMissile), __instance, "m_linearVelocity");
                    var mLinearVelocity = !mMissileAmmoDefinition.MissileSkipAcceleration
                        ? (Vector3) (currentLinearVelocity + __instance.PositionComp.WorldMatrixRef.Forward *
                            mMissileAmmoDefinition.MissileAcceleration * 0.0166666675359011)
                        : (Vector3) (__instance.WorldMatrix.Forward * (double) mMissileAmmoDefinition.DesiredSpeed);
                    SetInstanceField(typeof(MyMissile), __instance, "m_linearVelocity", mLinearVelocity);
                    if (mPhysicsEnabled)
                    {
                        __instance.Physics.LinearVelocity = mLinearVelocity;
                    }
                    else
                    {
                        Vector3.ClampToSphere(ref mLinearVelocity, mMissileAmmoDefinition.DesiredSpeed);
                        __instance.PositionComp.SetPosition(__instance.PositionComp.GetPosition() +
                                                            mLinearVelocity * 0.01666667f);
                    }

                    Vector3D mOrigin = (Vector3D) GetInstanceField(typeof(MyMissile), __instance, "m_origin");
                    float mMaxTrajectory = (float) GetInstanceField(typeof(MyMissile), __instance, "m_maxTrajectory");
                    if (Vector3.DistanceSquared(__instance.PositionComp.GetPosition(),
                        mOrigin) >= mMaxTrajectory * (double) mMaxTrajectory)
                    {
                        InvokeInstanceMethod(typeof(MyMissile), __instance, "MarkForExplosion", new Object[0]);
                    }

                    MyEntity entity;
                    MyHitInfo hitInfoRet;
                    Vector3D position = __instance.PositionComp.GetPosition();

                    MatrixD matrixD = __instance.PositionComp.WorldMatrixRef;
                    Vector3D to = matrixD.Translation +
                                  matrixD.Forward * (mMissileAmmoDefinition.MissileInitialSpeed / 15);

                    LineD line = new LineD(position, to);
                    var hitEntityIsNotNull = GetHitEntityAndPosition(line, out entity, out hitInfoRet);

                    if (!hitEntityIsNotNull)
                    {
                        // if (mMissileAmmoDefinition.Id.SubtypeName.Contains("Guided"))
                        // {
                        //     Vector3D guidedTo = matrixD.Translation + matrixD.Forward * mMissileAmmoDefinition.MaxTrajectory;
                        //     IMyEntity guidedEntity;
                        //     MyHitInfo guidedHitInfoRet;
                        //     LineD guidedline = new LineD(position, guidedTo);
                        //     var guidedHitEntityAndPositionNotNull =
                        //         GetHitEntityAndPosition(guidedline, out guidedEntity, out guidedHitInfoRet);
                        //     if (guidedHitEntityAndPositionNotNull)
                        //     {
                        //         var targetPosition = guidedEntity.GetPosition();
                        //         Vector3D aimDirection =   targetPosition - position;
                        //         MatrixD matrixD2 = __instance.PositionComp.WorldMatrix;
                        //         matrixD2.Forward = Vector3D.Normalize(aimDirection);
                        //     }
                        // }

                        return false;
                    }

                    double explosionOffcet = 0;
                    if (entity is MyCubeGrid)
                    {
                        explosionOffcet = ((MyCubeGrid) entity).GridSize / 5;
                    }

                    var missileExplosionPosition = hitInfoRet.Position;  // точка взрыва
                    //  - (line.Direction * explosionOffcet);
                    // __instance.PositionComp.SetPosition(missileExplosionPosition);
                    Log.Error("Explosion Position " + missileExplosionPosition);
                    SetInstanceField(typeof(MyMissile), __instance, "m_collidedEntity", entity);
                    entity.Pin();
                    SetInstanceField(typeof(MyMissile), __instance, "m_collisionPoint", hitInfoRet.Position);
                    SetInstanceField(typeof(MyMissile), __instance, "m_collisionNormal", hitInfoRet.Normal);

                    MySandboxGame.Static.Invoke(
                        (Action) (() =>
                            InvokeInstanceMethod(typeof(MyMissile), __instance, "MarkForExplosion", new Object[0])),
                        "MyMissile - collision invoke");
                    // Log.Error("missileExplosionPosition " + missileExplosionPosition);

                    BoundingSphereD explosionSphere = new BoundingSphereD(missileExplosionPosition,
                        mMissileAmmoDefinition.MissileExplosionRadius);
                    long OriginEntity = (long) GetInstanceField(typeof(MyMissile), __instance, "m_originEntity");

                    // explosionSphere.Radius *= 2.0;
                    var topMostEntitiesInSphere =
                        new HashSet<MyEntity>(MyEntities.GetTopMostEntitiesInSphere(ref explosionSphere));
                    // foreach (var myEntity in topMostEntitiesInSphere)
                    // {
                    //     Log.Error("myEntity in sphere " + myEntity.DisplayName);
                    // }
                    MyGridExplosion m_gridExplosion = ApplyVolumetricExplosionOnGrid(
                        mMissileAmmoDefinition.MissileExplosionDamage, ref explosionSphere, OriginEntity,
                        new List<MyEntity>(topMostEntitiesInSphere), line.Direction);
                    // foreach (var myEntity in topMostEntitiesInSphere)
                    // {
                    //     if (myEntity is MyCubeGrid)
                    //     {
                    //         Log.Error("MyCubeGrid procesing " + myEntity.DisplayName);
                    //         var myCubeGrid = (MyCubeGrid) myEntity;
                    //         m_gridExplosion.AffectedCubeGrids.Add(myCubeGrid);
                    //         BoundingSphereD sphere1 = new BoundingSphereD(explosionSphere.Center,
                    //             Math.Max(0.100000001490116, explosionSphere.Radius - (double) myCubeGrid.GridSize));
                    //         BoundingSphereD sphere2 =
                    //             new BoundingSphereD(explosionSphere.Center, explosionSphere.Radius);
                    //         BoundingSphereD sphere3 = new BoundingSphereD(explosionSphere.Center,
                    //             explosionSphere.Radius + (double) myCubeGrid.GridSize * 0.5 * Math.Sqrt(3.0));
                    //         MatrixD matrix = __instance.WorldMatrix;
                    //         Vector3D location = matrix.Translation;
                    //         AddGps("Center ", missileExplosionPosition);
                    //         AddGps("1 sphere ", missileExplosionPosition - line.Direction * sphere1.Radius);
                    //         AddGps("2 sphere ", missileExplosionPosition - line.Direction * sphere2.Radius);
                    //         AddGps("3 sphere ", missileExplosionPosition - line.Direction * sphere3.Radius);
                    //         HashSet<MySlimBlock> m_explodedBlocksInner = new HashSet<MySlimBlock>();
                    //         HashSet<MySlimBlock> m_explodedBlocksExact = new HashSet<MySlimBlock>();
                    //         HashSet<MySlimBlock> m_explodedBlocksOuter = new HashSet<MySlimBlock>();
                    //         float detectionBlockHalfSize = (float) ((double) myCubeGrid.GridSize / 2.0 / 1.25);
                    //         MatrixD invWorldGrid = myCubeGrid.PositionComp.WorldMatrixInvScaled;
                    //         myCubeGrid.GetBlocksInsideSpheres(ref sphere1, ref sphere2, ref sphere3,
                    //             m_explodedBlocksInner, m_explodedBlocksExact, m_explodedBlocksOuter, false,
                    //             detectionBlockHalfSize, ref invWorldGrid);
                    //         m_explodedBlocksInner.UnionWith((IEnumerable<MySlimBlock>) m_explodedBlocksExact);
                    //         m_gridExplosion.AffectedCubeBlocks.UnionWith(
                    //             (IEnumerable<MySlimBlock>) m_explodedBlocksInner);
                    //     }
                    // }

                    //m_gridExplosion.ComputeDamagedBlocks();
                    ComputeDamagedBlocks(m_gridExplosion);
                    var attackerId = GetInstanceField(typeof(MyMissile), __instance, "m_owner");
                    ApplyVolumetricDamageToGrid(m_gridExplosion, (long) attackerId, hitInfoRet);
                    missileExplosionPosition = hitInfoRet.Position
                                               - line.Direction *
                                               (mMissileAmmoDefinition.MissileInitialSpeed / 150);
                    __instance.PositionComp.SetPosition(missileExplosionPosition);
                }
            }
            catch (Exception e)
            {
                Log.Error("Missile target detection Exception ", e);
            }

            return false;
        }

        public static void ComputeDamagedBlocks(MyGridExplosion m_gridExplosion)
        {

            Dictionary<MySlimBlock, float> m_damagedBlocks = new Dictionary<MySlimBlock, float>();

            foreach (MySlimBlock affectedCubeBlock in m_gridExplosion.AffectedCubeBlocks)
            {
                Dictionary<MySlimBlock, MyGridExplosion.MyRaycastDamageInfo> m_damageRemaining =
                    new Dictionary<MySlimBlock, MyGridExplosion.MyRaycastDamageInfo>();
                Stack<MySlimBlock> m_castBlocks = new Stack<MySlimBlock>();
                MyGridExplosion.MyRaycastDamageInfo raycastDamageInfo =
                    CastDDA(affectedCubeBlock, m_castBlocks, m_damageRemaining, m_gridExplosion);
                while (m_castBlocks.Count > 0)
                {
                    MySlimBlock key = m_castBlocks.Pop();
                    if (key.FatBlock is MyWarhead)
                    {
                        m_damagedBlocks[key] = 1E+07f;
                    }
                    else
                    {
                        float blockCenterToExplosionCenter = (float) (key.WorldAABB.Center - m_gridExplosion.Sphere.Center).Length();
                        if ((double) raycastDamageInfo.DamageRemaining > 0.0)
                        {
                            // var sphereRadius = m_gridExplosion.Sphere.Radius; //4m
                            // var distanceBlockSurfaceToExplosion = raycastDamageInfo.DistanceToExplosion;  //3m
                            // if (distanceBlockSurfaceToExplosion < key.CubeGrid.GridSize / 2)
                            // {
                            //     m_damagedBlocks.Add(key, m_gridExplosion.Damage);
                            //     continue;
                            // }
                            // var damageMultiplier = 1 - (distanceBlockSurfaceToExplosion / sphereRadius);
                            // double damage = m_gridExplosion.Damage * damageMultiplier;
                            //
                            // if (raycastDamageInfo.DamageRemaining < damage)
                            // {
                            //     damage = raycastDamageInfo.DamageRemaining;
                            // }
                            // raycastDamageInfo.DamageRemaining = (float) (raycastDamageInfo.DamageRemaining - damage);
                            // m_damagedBlocks.Add(key, (float) damage);
                            // float num1 = (float) (key.WorldAABB.Center - m_gridExplosion.Sphere.Center).Length(); // 1.5m

                            float num2 =
                                MathHelper.Clamp(
                                    (float) (1.0 - ((double) blockCenterToExplosionCenter - (double) raycastDamageInfo.DistanceToExplosion) /
                                        (m_gridExplosion.Sphere.Radius -
                                         (double) raycastDamageInfo.DistanceToExplosion)), 0.0f, 1f);
                            if (m_damagedBlocks.ContainsKey(key))
                            {
                                continue;
                            }
                            if ((double) num2 > 0.0)
                            {
                                m_damagedBlocks.Add(key,
                                    raycastDamageInfo.DamageRemaining * num2 * key.BlockDefinition.GeneralDamageMultiplier);
                                raycastDamageInfo.DamageRemaining = Math.Max(0.0f,
                                    (float) ((double) raycastDamageInfo.DamageRemaining * (double) num2 -
                                             (double) key.Integrity ));
                            }
                            else
                                m_damagedBlocks.Add(key, raycastDamageInfo.DamageRemaining);
                        }
                        else
                            raycastDamageInfo.DamageRemaining = 0.0f;

                        raycastDamageInfo.DistanceToExplosion = Math.Abs(blockCenterToExplosionCenter);
                        m_damageRemaining.Add(key, raycastDamageInfo);
                    }
                }
            }
            //m_gridExplosion.DamagedBlocks.Clear();
            foreach (var mDamagedBlock in m_damagedBlocks)
            {
                m_gridExplosion.DamagedBlocks.Add(mDamagedBlock.Key, mDamagedBlock.Value);
            }
        }

        private static MyGridExplosion.MyRaycastDamageInfo CastDDA(MySlimBlock cubeBlock,
            Stack<MySlimBlock> m_castBlocks,
            Dictionary<MySlimBlock,
                MyGridExplosion.MyRaycastDamageInfo> m_damageRemaining, MyGridExplosion m_gridExplosion)
        {
            if (m_damageRemaining.ContainsKey(cubeBlock))
                return m_damageRemaining[cubeBlock];
            int stackOverflowGuard = 0;
            m_castBlocks.Push(cubeBlock);
            Vector3D worldCenter;
            cubeBlock.ComputeWorldCenter(out worldCenter);
            List<Vector3I> m_cells = new List<Vector3I>();
            // this.m_cells.Clear();
            cubeBlock.CubeGrid.RayCastCells(worldCenter, m_gridExplosion.Sphere.Center, m_cells, new Vector3I?(),
                false, true);
            (m_gridExplosion.Sphere.Center - worldCenter).Normalize();
            foreach (Vector3I cell in m_cells)
            {
                Vector3D fromWorldPos =
                    Vector3D.Transform(cell * cubeBlock.CubeGrid.GridSize, cubeBlock.CubeGrid.WorldMatrix);
                int num = MyDebugDrawSettings.DEBUG_DRAW_EXPLOSION_DDA_RAYCASTS ? 1 : 0;
                MySlimBlock cubeBlock1 = cubeBlock.CubeGrid.GetCubeBlock(cell);
                if (cubeBlock1 == null)
                    return IsExplosionInsideCell(cell, cubeBlock.CubeGrid, m_gridExplosion)
                        ? new MyGridExplosion.MyRaycastDamageInfo(m_gridExplosion.Damage,
                            (float) (fromWorldPos - m_gridExplosion.Sphere.Center).Length())
                        : CastPhysicsRay(fromWorldPos, ref stackOverflowGuard, m_gridExplosion, m_castBlocks, m_damageRemaining);
                if (cubeBlock1 != cubeBlock)
                {
                    if (m_damageRemaining.ContainsKey(cubeBlock1))
                        return m_damageRemaining[cubeBlock1];
                    if (!m_castBlocks.Contains(cubeBlock1))
                        m_castBlocks.Push(cubeBlock1);
                }
                else if (IsExplosionInsideCell(cell, cubeBlock.CubeGrid, m_gridExplosion))
                    return new MyGridExplosion.MyRaycastDamageInfo(m_gridExplosion.Damage,
                        (float) (fromWorldPos - m_gridExplosion.Sphere.Center).Length());
            }

            return new MyGridExplosion.MyRaycastDamageInfo(m_gridExplosion.Damage,
                (float) (worldCenter - m_gridExplosion.Sphere.Center).Length());
        }

        private static bool IsExplosionInsideCell(Vector3I cell, MyCubeGrid cellGrid, MyGridExplosion m_gridExplosion) =>
            cellGrid.WorldToGridInteger(m_gridExplosion.Sphere.Center) == cell;

        private static MyGridExplosion.MyRaycastDamageInfo CastPhysicsRay(Vector3D fromWorldPos,
            ref int stackOverflowGuard, MyGridExplosion m_gridExplosion, Stack<MySlimBlock> m_castBlocks,
            Dictionary<MySlimBlock, MyGridExplosion.MyRaycastDamageInfo> m_damageRemaining)
        {
            Vector3D position = Vector3D.Zero;
            IMyEntity myEntity = (IMyEntity) null;
            MyPhysics.HitInfo? nullable = MyPhysics.CastRay(fromWorldPos, m_gridExplosion.Sphere.Center, 29);
            if (nullable.HasValue)
            {
                myEntity = nullable.Value.HkHitInfo.Body.UserObject != null
                    ? ((MyPhysicsComponentBase) nullable.Value.HkHitInfo.Body.UserObject).Entity
                    : (IMyEntity) null;
                position = nullable.Value.Position;
            }

            Vector3D normal = m_gridExplosion.Sphere.Center - fromWorldPos;
            float distanceToExplosion = (float) normal.Normalize();
            MyCubeGrid myCubeGrid = null;
            if (!(myEntity is MyCubeGrid) && myEntity is MyCubeBlock myCubeBlock)
            {
                myCubeGrid = myCubeBlock.CubeGrid;
            }
            else if (myEntity is MyCubeGrid)
            {
                myCubeGrid = (MyCubeGrid) myEntity;
            }
            if (myCubeGrid != null)
            {
                Vector3D vector3D1 = Vector3D.Transform(position, myCubeGrid.PositionComp.WorldMatrixNormalizedInv) *
                                     (double) myCubeGrid.GridSizeR;
                Vector3D vector3D2 =
                    Vector3D.TransformNormal(normal, myCubeGrid.PositionComp.WorldMatrixNormalizedInv) * 1.0 / 8.0;
                for (int index = 0; index < 5; ++index)
                {
                    Vector3I pos = Vector3I.Round(vector3D1);
                    MySlimBlock cubeBlock = myCubeGrid.GetCubeBlock(pos);
                    if (cubeBlock != null)
                        return m_castBlocks.Contains(cubeBlock)
                            ? new MyGridExplosion.MyRaycastDamageInfo(0.0f, distanceToExplosion)
                            : CastDDA(cubeBlock, m_castBlocks, m_damageRemaining, m_gridExplosion);
                    vector3D1 += vector3D2;
                }

                Vector3D fromWorldPos1 =
                    Vector3D.Transform(vector3D1 * (double) myCubeGrid.GridSize, myCubeGrid.WorldMatrix);
                if (new BoundingBoxD(Vector3D.Min(fromWorldPos, fromWorldPos1),
                        Vector3D.Max(fromWorldPos, fromWorldPos1)).Contains(m_gridExplosion.Sphere.Center) ==
                    ContainmentType.Contains)
                    return new MyGridExplosion.MyRaycastDamageInfo(m_gridExplosion.Damage, distanceToExplosion);
                ++stackOverflowGuard;
                if (stackOverflowGuard > 10)
                {
                    int num = MyDebugDrawSettings.DEBUG_DRAW_EXPLOSION_HAVOK_RAYCASTS ? 1 : 0;
                    return new MyGridExplosion.MyRaycastDamageInfo(0.0f, distanceToExplosion);
                }

                int num1 = MyDebugDrawSettings.DEBUG_DRAW_EXPLOSION_HAVOK_RAYCASTS ? 1 : 0;
                return CastPhysicsRay(fromWorldPos1, ref stackOverflowGuard, m_gridExplosion, m_castBlocks, m_damageRemaining);
            }

            if (!nullable.HasValue)
                return new MyGridExplosion.MyRaycastDamageInfo(m_gridExplosion.Damage, distanceToExplosion);
            int num2 = MyDebugDrawSettings.DEBUG_DRAW_EXPLOSION_HAVOK_RAYCASTS ? 1 : 0;
            return new MyGridExplosion.MyRaycastDamageInfo(0.0f, distanceToExplosion);
        }

        private static MyGridExplosion ApplyVolumetricExplosionOnGrid(
            float MissileExplosionDamage,
            ref BoundingSphereD sphere,
            long OriginEntity,
            List<MyEntity> entities,
            Vector3D Direction)
        {
            MyGridExplosion m_gridExplosion = new MyGridExplosion();
            m_gridExplosion.Init(sphere, MissileExplosionDamage);
            var Node2 = (MyCubeGrid) null;
            MyGroups<MyCubeGrid, MyGridLogicalGroupData>.Group group =
                (MyGroups<MyCubeGrid, MyGridLogicalGroupData>.Group) null;
            if (!MySession.Static.Settings.EnableTurretsFriendlyFire && OriginEntity != 0L)
            {
                MyEntity entityById = MyEntities.GetEntityById(OriginEntity);
                if (entityById != null)
                {
                    var topMostParent = entityById.GetTopMostParent((Type) null);
                    if (topMostParent is MyCubeGrid)
                    {
                        Node2 = (MyCubeGrid) topMostParent;
                        group = MyCubeGridGroups.Static.Logical.GetGroup((MyCubeGrid) topMostParent);
                    }
                }
            }

            foreach (MyEntity entity in entities)
            {
                // Log.Error("Process Entity " + entity.DisplayName);
                if (
                    // entity != explosionInfo.ExcludedEntity &&
                    entity is MyCubeGrid Node &&
                    (Node.CreatePhysics && Node != Node2) &&
                    (group == null || MyCubeGridGroups.Static.Logical.GetGroup(Node) != group))
                {
                    m_gridExplosion.AffectedCubeGrids.Add(Node);
                    float detectionBlockHalfSize = (float) ((double) Node.GridSize / 2.0 / 1.25);
                    MatrixD invWorldGrid = Node.PositionComp.WorldMatrixInvScaled;
                    BoundingSphereD sphere1 = new BoundingSphereD(sphere.Center,
                        Math.Max(0.100000001490116, sphere.Radius - (double) Node.GridSize));
                    // BoundingSphereD sphere1 = new BoundingSphereD(sphere.Center,
                    //     0.100000001490116);
                    BoundingSphereD sphere2 = new BoundingSphereD(sphere.Center, sphere.Radius);
                    BoundingSphereD sphere3 = new BoundingSphereD(sphere.Center,
                        sphere.Radius + (double) Node.GridSize * 0.5 * Math.Sqrt(3.0));
                    AddGps("Center ", sphere.Center);
                    AddGps("1 sphere ", sphere.Center - Direction * sphere1.Radius);
                    AddGps("2 sphere ", sphere.Center - Direction * sphere2.Radius);
                    AddGps("3 sphere ", sphere.Center - Direction * sphere3.Radius);
                    HashSet<MySlimBlock> m_explodedBlocksInner = new HashSet<MySlimBlock>();
                    HashSet<MySlimBlock> m_explodedBlocksExact = new HashSet<MySlimBlock>();
                    HashSet<MySlimBlock> m_explodedBlocksOuter = new HashSet<MySlimBlock>();
                    Node.GetBlocksInsideSpheres(ref sphere1, ref sphere2, ref sphere3, m_explodedBlocksInner,
                        m_explodedBlocksExact, m_explodedBlocksOuter, false, detectionBlockHalfSize,
                        ref invWorldGrid);
                    m_explodedBlocksInner.UnionWith((IEnumerable<MySlimBlock>) m_explodedBlocksExact);
                    m_gridExplosion.AffectedCubeBlocks.UnionWith(
                        (IEnumerable<MySlimBlock>) m_explodedBlocksInner);
                    foreach (MySlimBlock block in m_explodedBlocksOuter)
                        Node.Physics.AddDirtyBlock(block);
                    m_explodedBlocksInner.Clear();
                    m_explodedBlocksExact.Clear();
                    m_explodedBlocksOuter.Clear();
                }
            }

            return m_gridExplosion;
        }

        private static void AddGps(string message, Vector3D? asteroidPosition)
        {
            try
            {
                var asteroidGPS =
                    MyAPIGateway.Session?.GPS.Create(message, message, asteroidPosition.Value, true);
                asteroidGPS.GPSColor = Color.Red;
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                foreach (var myPlayer in players)
                {
                    MyAPIGateway.Session?.GPS.AddGps(myPlayer.IdentityId, asteroidGPS);
                }
            }
            catch (Exception e)
            {
                Log.Error("AddGps Exception ", e);
            }
        }

        private static void ApplyVolumetricDamageToGrid(MyGridExplosion damageInfo, long attackerId,
            MyHitInfo hitInfoRet)
        {
            Dictionary<MySlimBlock, float> damagedBlocks = damageInfo.DamagedBlocks;
            HashSet<MySlimBlock> affectedCubeBlocks = damageInfo.AffectedCubeBlocks;
            HashSet<MyCubeGrid> affectedCubeGrids = damageInfo.AffectedCubeGrids;

            bool anyBeforeHandler = MyDamageSystem.Static.HasAnyBeforeHandler;
            foreach (KeyValuePair<MySlimBlock, float> keyValuePair in damagedBlocks)
            {
                MySlimBlock key = keyValuePair.Key;
                if (!key.CubeGrid.MarkedForClose && (key.FatBlock == null || !key.FatBlock.MarkedForClose) &&
                    (!key.IsDestroyed && key.CubeGrid.BlocksDestructionEnabled))
                {
                    float amount = keyValuePair.Value;
                    if (anyBeforeHandler && key.UseDamageSystem)
                    {
                        MyDamageInformation info =
                            new MyDamageInformation(false, amount, MyDamageType.Explosion, attackerId);
                        MyDamageSystem.Static.RaiseBeforeDamageApplied((object) key, ref info);
                        if ((double) info.Amount > 0.0)
                            amount = info.Amount;
                        else
                            continue;
                    }

                    if (affectedCubeBlocks.Contains(keyValuePair.Key) && !true)
                    {
                        key.CubeGrid.RemoveDestroyedBlock(key, 0L);
                    }
                    else
                    {
                        if (key.FatBlock == null &&
                            (double) key.Integrity / (double) key.DeformationRatio < (double) amount
                            // || key.FatBlock == this.m_explosionInfo.HitEntity
                        )
                        {
                            key.CubeGrid.RemoveDestroyedBlock(key, 0L);
                        }
                        else
                        {
                            if (key.FatBlock != null)
                                amount *= 7f;
                            key.DoDamage(amount, MyDamageType.Explosion, true, null, attackerId: attackerId);
                            if (!key.IsDestroyed)
                                key.CubeGrid.ApplyDestructionDeformation(key, 1f, new MyHitInfo?(), 0L);
                        }

                        foreach (MySlimBlock neighbour in key.Neighbours)
                            neighbour.CubeGrid.Physics.AddDirtyBlock(neighbour);
                        key.CubeGrid.Physics.AddDirtyBlock(key);
                    }
                }
            }
        }

        private static bool RegisterMissilePatched(MyMissile missile)
        {
            return false;
        }

        private static bool MoveMissilePatched(MyMissile missile, ref Vector3 velocity)
        {
            return false;
        }

        private static void DoDamageInternalPatched(MySlimBlock __instance, float damage,
            MyStringHash damageType,
            bool addDirtyParts = true,
            MyHitInfo? hitInfo = null,
            long attackerId = 0)
        {
            var instanceFatBlock = __instance.FatBlock;
            if (instanceFatBlock == null)
            {
                Log.Error("instanceFatBlock == null ");
                return;
            }

            Log.Error(
                "MySlimBlock " + instanceFatBlock.DisplayName + " damage " + damage + " damage type " + damageType);
        }

        private static void ApplyAccumulatedDamagePatched(bool addDirtyParts = true, long attackerId = 0)
        {
            Log.Error("ApplyAccumulatedDamagePatched");
        }

        private static void MyComponentStackApplyDamageMethodPatched(float damage)
        {
            Log.Error("MyComponentStackApplyDamageMethodPatched " + damage);
        }

        private static bool ExecuteExplosionMethodPatched(MyMissile __instance)
        {
            MyMissileAmmoDefinition mMissileAmmoDefinition =
                (MyMissileAmmoDefinition) GetInstanceField(typeof(MyMissile), __instance,
                    "m_missileAmmoDefinition");
            InvokeInstanceMethod(typeof(MyMissile), __instance, "PlaceDecal", new Object[0]);
            var instanceField = GetInstanceField(typeof(MyMissile), __instance, "m_origin");
            float missileExplosionRadius = mMissileAmmoDefinition.MissileExplosionRadius;
            BoundingSphereD boundingSphereD =
                new BoundingSphereD(__instance.PositionComp.GetPosition(), (double) missileExplosionRadius);
            MyExplosionInfo explosionInfo = new MyExplosionInfo()
            {
                PlayerDamage = 0.0f,
                Damage = 0.0f,
                ExplosionType = MyExplosionTypeEnum.MISSILE_EXPLOSION,
                ExplosionSphere = boundingSphereD,
                LifespanMiliseconds = 700,
                ParticleScale = 1f,
                Direction = new Vector3?(
                    Vector3.Normalize(__instance.PositionComp.GetPosition() - (Vector3D) instanceField)),
                VoxelExplosionCenter = boundingSphereD.Center +
                                       (double) missileExplosionRadius * __instance.WorldMatrix.Forward * 0.25,
                ExplosionFlags = MyExplosionFlags.AFFECT_VOXELS | MyExplosionFlags.CREATE_DECALS |
                                 MyExplosionFlags.CREATE_PARTICLE_EFFECT | MyExplosionFlags.CREATE_SHRAPNELS |
                                 MyExplosionFlags.CREATE_PARTICLE_DEBRIS,
                VoxelCutoutScale = 0.3f,
                PlaySound = true,
                ApplyForceAndDamage = false,
                KeepAffectedBlocks = true
            };
            MyExplosions.AddExplosion(ref explosionInfo);
            InvokeInstanceMethod(typeof(MyMissile), __instance, "Return", new Object[0]);
            return false;
        }

        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }

        internal static void SetInstanceField(Type type, object instance, string fieldName, Object value)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            field.SetValue(instance, value);
        }

        internal static void InvokeInstanceMethod(Type type, object instance, string methodName, Object[] args)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Static;
            var method = type.GetMethod(methodName, bindFlags);
            method.Invoke(instance, args);
        }


        private static bool GetHitEntityAndPosition(
            LineD line,
            out MyEntity entity,
            out MyHitInfo hitInfoRet)
        {
            entity = null;
            hitInfoRet = new MyHitInfo();

            int index = 0;
            List<MyPhysics.HitInfo> mRaycastResult = new List<MyPhysics.HitInfo>();
            using (MyUtils.ReuseCollection(ref mRaycastResult))
            {
                MyPhysics.CastRay(line.From, line.To, mRaycastResult, 15);
                do
                {
                    if (index < mRaycastResult.Count)
                    {
                        MyPhysics.HitInfo hitInfo = mRaycastResult[index];
                        entity = (hitInfo.HkHitInfo.GetHitEntity() as MyEntity);
                        hitInfoRet.Position = hitInfo.Position;
                        hitInfoRet.Normal = hitInfo.HkHitInfo.Normal;
                        hitInfoRet.ShapeKey = hitInfo.HkHitInfo.GetShapeKey(0);
                    }

                    MyCharacterHitInfo mCharHitInfo = null;
                    if (entity is MyCharacter myCharacter && !Game.IsDedicated)
                    {
                        if (!myCharacter.GetIntersectionWithLine(ref line, ref mCharHitInfo))
                            entity = null;
                    }
                    else if (entity is MyCubeGrid myCubeGrid)
                    {
                        MyCubeGrid.MyCubeGridHitInfo mCubeGridHitInfo = null;
                        if (myCubeGrid.GetIntersectionWithLine(ref line, ref mCubeGridHitInfo))
                        {
                            hitInfoRet.Position = mCubeGridHitInfo.Triangle.IntersectionPointInWorldSpace;
                            hitInfoRet.Normal = mCubeGridHitInfo.Triangle.NormalInWorldSpace;
                            if (Vector3.Dot(hitInfoRet.Normal, line.Direction) > 0.0)
                                hitInfoRet.Normal = -hitInfoRet.Normal;
                        }

                        if (mCubeGridHitInfo != null &&
                            mCubeGridHitInfo.Triangle.UserObject is MyCube userObject &&
                            (userObject.CubeBlock.FatBlock != null && userObject.CubeBlock.FatBlock.Physics == null))
                            entity = userObject.CubeBlock.FatBlock;
                    }
                    else
                    {
                        MyIntersectionResultLineTriangleEx? t;
                        if (entity is MyVoxelBase myVoxelBase &&
                            myVoxelBase.GetIntersectionWithLine(ref line, out t, IntersectionFlags.DIRECT_TRIANGLES))
                        {
                            hitInfoRet.Position = t.Value.IntersectionPointInWorldSpace;
                            hitInfoRet.Normal = t.Value.NormalInWorldSpace;
                            hitInfoRet.ShapeKey = 0U;
                        }
                    }

                    if (entity != null)
                        break;
                } while (++index < mRaycastResult.Count);
            }

            return entity != null;
        }
    }
}