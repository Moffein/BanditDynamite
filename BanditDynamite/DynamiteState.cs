using EntityStates;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EntityStates.Moffein.BanditDynamite
{
    public class ClusterBomb : BaseState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = ClusterBomb.baseDuration / this.attackSpeedStat;
            Ray aimRay = base.GetAimRay();
            base.StartAimMode(aimRay, 2f, false);
            base.PlayAnimation("Gesture, Additive", "SlashBlade", "SlashBlade.playbackRate", this.duration);
            Util.PlaySound("Play_MoffeinBanditDynamite_toss", base.gameObject);
            if (base.isAuthority)
            {
                ProjectileManager.instance.FireProjectile(ClusterBomb.projectilePrefab, aimRay.origin, Util.QuaternionSafeLookRotation(aimRay.direction), base.gameObject, this.damageStat * ClusterBomb.damageCoefficient, 0f, Util.CheckRoll(this.critStat, base.characterBody.master), DamageColorIndex.Default, null, -1f);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (base.inputBank && base.inputBank.skill2.down)
            {
                return InterruptPriority.PrioritySkill;
            }
            return InterruptPriority.Skill;
        }

        public static GameObject projectilePrefab;
        public static float damageCoefficient;
        public static float force = 2500f;
        public static float baseDuration;
        public static float bombletDamageCoefficient;
        private float duration;
        public static bool quickdrawEnabled = false;
    }
}