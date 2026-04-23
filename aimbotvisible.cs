using ImGuiNET;
using System;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;

namespace SRDIVINE
{
    internal static class SRDIVINE_Aimbot
    {
        private static CancellationTokenSource _divineCancelToken = new();
        private static bool _divineIsActive = false;
        private static readonly Random _divineRandom = new Random();
        private static int _divineShotCounter = 0;
        private static int _divineMissCounter = 0;

        // Headshot rate control using slidersss!! 
        private static int _divineHeadshotRate = 100;
        private static readonly object _divineLockObject = new object();

        internal static void Divine_StartWork()
        {
            while (true)
            {
                if (!Config.SR_DIVINE_AimbotEnabled)
                {
                    Thread.Sleep(2);
                    continue;
                }

                if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                {
                    Thread.Sleep(1);
                    continue;
                }

                Entity divineTarget = Divine_FindOptimalTarget();
                if (divineTarget != null)
                {
                    Divine_ExecuteAimLock(divineTarget);
                }
            }
        }

        private static Entity Divine_FindOptimalTarget()
        {
            Entity divineOptimalTarget = null;
            float divineClosestRange = float.MaxValue;
            var divineScreenOrigin = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var divineEntity in Core.Entities.Values)
            {
                if (divineEntity.IsDead) continue;

                if (Config.SR_DIVINE_IgnoreKnocked && divineEntity.IsKnocked)
                    continue;

                var divineHeadPosition = W2S.WorldToScreen(Core.CameraMatrix, divineEntity.Head, Core.Width, Core.Height);
                if (divineHeadPosition.X < 1 || divineHeadPosition.Y < 1) continue;

                float divineTargetDistance = Vector3.Distance(Core.LocalMainCamera, divineEntity.Head);
                if (divineTargetDistance > Config.SR_DIVINE_MaxDistance) continue;

                float divineCrosshairOffset = Vector2.Distance(divineScreenOrigin, divineHeadPosition);
                if (divineCrosshairOffset < divineClosestRange && divineCrosshairOffset <= Config.SR_DIVINE_FOVRadius)
                {
                    divineClosestRange = divineCrosshairOffset;
                    divineOptimalTarget = divineEntity;
                    Thread.Sleep(Config.SR_DIVINE_SmoothnessFactor);
                }
            }

            return divineOptimalTarget;
        }

        private static void Divine_ExecuteAimLock(Entity divineEntity)
        {
            if (divineEntity == null || divineEntity.Address == 0) return;

            uint divineHeadColliderID;
            var divineReadSuccess = InternalMemory.Read<uint>(divineEntity.Address + 0x4A8, out divineHeadColliderID);
            if (!divineReadSuccess || divineHeadColliderID == 0) return;

            lock (_divineLockObject)
            {
                _divineShotCounter++;
                
                // Apply headshot control
                bool divineShouldHitHead = Divine_CalculateHeadshotDecision();
                
                uint divineTargetBoneID;
                if (divineShouldHitHead)
                {
                    divineTargetBoneID = divineHeadColliderID; // Head
                }
                else
                {
                    divineTargetBoneID = Divine_GetBodyBoneID(divineEntity); // Body 
                }

                const int divineWriteIterations = 10;
                for (int divineIndex = 0; divineIndex < divineWriteIterations; divineIndex++)
                {
                    InternalMemory.Write(divineEntity.Address + Offsets.SRDIVINE, divineTargetBoneID); // CHANGE SRDIVINE INTO YOUR CLASS NAME OT UPDATE THE CLASS NAME TO SRDIVINE
                }
                InternalMemory.Write(divineEntity.Address + Offsets.SRDIVINE, divineTargetBoneID);// SAME DO IT HERE
            }
        }

        private static bool Divine_CalculateHeadshotDecision()
        {
            int divineCurrentRate;
            lock (_divineLockObject)
            {
                divineCurrentRate = _divineHeadshotRate;
            }

            // 100% headshot 
            if (divineCurrentRate >= 100)
                return true;

            // 0% headshot 
            if (divineCurrentRate <= 0)
                return false;

            // adujust with respect to percentages! 
            int divineRoll = _divineRandom.Next(0, 100);
            bool divineIsHeadshot = divineRoll < divineCurrentRate;

            lock (_divineLockObject)
            {
                if (divineIsHeadshot)
                {
                    _divineMissCounter = 0;
                }
                else
                {
                    _divineMissCounter++;
                    //force headshot used here! 
                    if (_divineMissCounter > (100 - divineCurrentRate) / 10 + 3)
                    {
                        _divineMissCounter = 0;
                        return true;
                    }
                }
            }

            return divineIsHeadshot;
        }

        private static uint Divine_GetBodyBoneID(Entity divineEntity)
        {
            //try chest here
            uint divineChestBone;
            if (InternalMemory.Read<uint>(divineEntity.Address + 0x4A0, out divineChestBone) && divineChestBone != 0)
                return divineChestBone;

            //  penis
            uint divinePelvisBone;
            if (InternalMemory.Read<uint>(divineEntity.Address + 0x498, out divinePelvisBone) && divinePelvisBone != 0)
                return divinePelvisBone;

            //  neck
            uint divineNeckBone;
            if (InternalMemory.Read<uint>(divineEntity.Address + 0x4A4, out divineNeckBone) && divineNeckBone != 0)
                return divineNeckBone;

            return 0;
        }
//small api like to controol headshot rate
        public static void Divine_SetHeadshotRate(int divineRatePercent)
        {
            lock (_divineLockObject)
            {
                _divineHeadshotRate = Math.Clamp(divineRatePercent, 0, 100);
            }
        }

        public static int Divine_GetHeadshotRate()
        {
            lock (_divineLockObject)
            {
                return _divineHeadshotRate;
            }
        }

        public static (int shots, int headshots, float percentage) Divine_GetStatistics()
        {
            lock (_divineLockObject)
            {
                int divineHeadshots = _divineShotCounter - _divineMissCounter;
                float divinePercentage = _divineShotCounter > 0 
                    ? (float)divineHeadshots / _divineShotCounter * 100f 
                    : 0f;
                return (_divineShotCounter, divineHeadshots, divinePercentage);
            }
        }

        public static void Divine_ResetStatistics()
        {
            lock (_divineLockObject)
            {
                _divineShotCounter = 0;
                _divineMissCounter = 0;
            }
        }

        internal static void Divine_RenderOverlay()
        {
            if (!Config.SR_DIVINE_AimbotEnabled || Core.Width == -1 || Core.Height == -1)
                return;

            var divineScreenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);
//small fov circle

            ImGui.GetBackgroundDrawList().AddCircle(
                divineScreenCenter,
                Config.SR_DIVINE_FOVRadius,
                ImGui.GetColorU32(new Vector4(0.8f, 0.2f, 0.8f, 0.7f)), // pink text for SR DIVINE
                64,
                2.5f
            );

            // it creates a small circle for indicating headshot rate
            float divineRateRadius = (Config.SR_DIVINE_FOVRadius * Divine_GetHeadshotRate()) / 100f;
            ImGui.GetBackgroundDrawList().AddCircle(
                divineScreenCenter,
                divineRateRadius,
                ImGui.GetColorU32(new Vector4(1f, 0.8f, 0f, 0.5f)), //Gold
                32,
                1.5f
            );

            var divineTextPos = new Vector2(divineScreenCenter.X + Config.SR_DIVINE_FOVRadius + 5, divineScreenCenter.Y - 10);
            ImGui.GetBackgroundDrawList().AddText(
                divineTextPos,
                ImGui.GetColorU32(new Vector4(1f, 0.8f, 0f, 1f)),
                $"SR DIVINE HS: {Divine_GetHeadshotRate()}%"
            );
        }

        internal static void Divine_ForceStop()
        {
            if (!_divineIsActive) return;

            _divineCancelToken.Cancel();
            _divineCancelToken = new CancellationTokenSource();
            _divineIsActive = false;
        }
    }
} /*Update your offsets.cs class name to SRDIVINE or replace your offset class name here instead of SRDIVINE. */