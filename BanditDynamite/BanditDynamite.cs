using BanditDynamite.Components;
using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using EntityStates.Moffein.BanditDynamite;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BanditDynamite
{
    [BepInDependency("com.bepis.r2api")]
    [R2API.Utils.R2APISubmoduleDependency(nameof(LanguageAPI),  nameof(PrefabAPI), nameof(SoundAPI), nameof(DamageAPI))]
    [BepInPlugin("com.Moffein.BanditDynamite", "Bandit Dynamite", "1.0.8")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class BanditDynamite : BaseUnityPlugin
    {
        AssetBundle assets;
        private readonly Shader hotpoo = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/hgstandard");
        public static GameObject ClusterBombObject;
        public static GameObject ClusterBombletObject;

        public static DamageAPI.ModdedDamageType ClusterBombDamage;

        public static float cbRadius, cbBombletRadius, cbBombletProcCoefficient, cbCooldown;
        public static int cbBombletCount, cbStock;
        bool disableFalloff = false;

        public void Awake()
        {
            ReadConfig();
            LoadResources();
            SetupClusterBomb();
            SetupClusterBomblet();
            RegisterLanguageTokens();
            AddSkill();

            FireBomblets.AddHook();

            On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
            {
                bool isDynamiteBundle = false;
                bool banditAttacker = false;
                bool resetCooldown = (damageInfo.damageType & DamageType.ResetCooldownsOnKill) > 0 || (damageInfo.damageType & DamageType.GiveSkullOnKill) > 0;
                AssignDynamiteTeamFilter ad = self.gameObject.GetComponent<AssignDynamiteTeamFilter>();
                if (ad)
                {
                    isDynamiteBundle = true;
                }

                CharacterBody attackerCB = null;
                if (damageInfo.attacker)
                {
                    attackerCB = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerCB)
                    {
                        banditAttacker = attackerCB.bodyIndex == BodyCatalog.FindBodyIndex("Bandit2Body");
                    }
                }

                if (isDynamiteBundle)
                {
                    if (!ad.fired && banditAttacker && (damageInfo.damageType & DamageType.AOE) == 0 && damageInfo.procCoefficient > 0f)
                    {
                        ad.fired = true;
                        damageInfo.crit = true;
                        damageInfo.procCoefficient = 0f;
                        ProjectileImpactExplosion pie = self.gameObject.GetComponent<ProjectileImpactExplosion>();
                        if (pie)
                        {
                            pie.blastRadius *= 2f;
                            pie.falloffModel = BlastAttack.FalloffModel.None;
                        }

                        ProjectileDamage pd = self.gameObject.GetComponent<ProjectileDamage>();
                        if (pd)
                        {
                            if (resetCooldown)
                            {
                                pd.damage *= 2f;

                                damageInfo.damageType &= ~DamageType.ResetCooldownsOnKill;
                                damageInfo.damageType &= ~DamageType.GiveSkullOnKill;

                                BanditNetworkCommands bnc = damageInfo.attacker.GetComponent<BanditNetworkCommands>();
                                if (bnc)
                                {
                                    bnc.RpcResetSpecialCooldown();
                                }
                            }
                            else
                            {
                                pd.damage *= 1.5f;
                            }
                        }
                    }
                    else
                    {
                        damageInfo.rejected = true;
                    }
                }

                orig(self, damageInfo);
            };
        }

        public void RegisterLanguageTokens()
        {
            LanguageAPI.Add("MOFFEINBANDITDYNAMITE_SECONDARY_NAME", "Dynamite Toss");
            LanguageAPI.Add("MOFFEINBANDITDYNAMITE_SECONDARY_DESC", "Toss a bomb that <style=cIsDamage>ignites</style> for <style=cIsDamage>" + (ClusterBomb.damageCoefficient).ToString("P0").Replace(" ", "").Replace(",", "") + " damage</style>."
                + " Drops bomblets for <style=cIsDamage>" + cbBombletCount + "x"
                + (ClusterBomb.bombletDamageCoefficient).ToString("P0").Replace(" ", "").Replace(",", "") + " damage</style>."
                + " Can be shot midair for <style=cIsDamage>bonus damage</style>." + Environment.NewLine);
        }

        private void AddSkill()
        {
            SkillDef clusterBombDef = SkillDef.CreateInstance<SkillDef>();
            clusterBombDef.activationState = new SerializableEntityStateType(typeof(ClusterBomb));
            clusterBombDef.baseRechargeInterval = cbCooldown;
            clusterBombDef.skillNameToken = "MOFFEINBANDITDYNAMITE_SECONDARY_NAME";
            clusterBombDef.skillDescriptionToken = "MOFFEINBANDITDYNAMITE_SECONDARY_DESC";
            clusterBombDef.skillName = "Dynamite";
            clusterBombDef.icon = assets.LoadAsset<Sprite>("dynamite_red.png");
            clusterBombDef.baseMaxStock = cbStock;
            clusterBombDef.rechargeStock = 1;
            clusterBombDef.beginSkillCooldownOnSkillEnd = false;
            clusterBombDef.activationStateMachineName = "Weapon";
            clusterBombDef.interruptPriority = InterruptPriority.Skill;
            clusterBombDef.isCombatSkill = true;
            clusterBombDef.cancelSprintingOnActivation = false;
            clusterBombDef.canceledFromSprinting = false;
            clusterBombDef.mustKeyPress = false;
            clusterBombDef.requiredStock = 1;
            clusterBombDef.stockToConsume = 1;
            clusterBombDef.keywordTokens = new string[] { };
            R2API.ContentAddition.AddSkillDef(clusterBombDef);
            R2API.ContentAddition.AddEntityState<ClusterBomb>(out bool wasAdded);

            GameObject banditObject = LegacyResourcesAPI.Load<GameObject>("prefabs/characterbodies/Bandit2Body");
            banditObject.AddComponent<BanditNetworkCommands>();

            SkillFamily secondarySkillFamily = banditObject.GetComponent<SkillLocator>().secondary.skillFamily;
            Array.Resize(ref secondarySkillFamily.variants, secondarySkillFamily.variants.Length + 1);
            secondarySkillFamily.variants[secondarySkillFamily.variants.Length - 1] = new SkillFamily.Variant
            {
                skillDef = clusterBombDef,
                unlockableName = "",
                viewableNode = new ViewablesCatalog.Node(clusterBombDef.skillNameToken, false)
            };
        }

        private void ReadConfig()
        {
            ClusterBomb.damageCoefficient = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Damage*"), 3.9f, new ConfigDescription("How much damage Dynamite Toss deals.")).Value;
            cbRadius = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Radius*"), 8f, new ConfigDescription("How large the explosion is. Radius is doubled when shot out of the air.")).Value;
            cbBombletCount = base.Config.Bind<int>(new ConfigDefinition("Dynamite", "Bomblet Count*"), 6, new ConfigDescription("How many mini bombs Dynamite Toss releases.")).Value;
            ClusterBomb.bombletDamageCoefficient = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Bomblet Damage*"), 1.2f, new ConfigDescription("How much damage Dynamite Toss Bomblets deals.")).Value;
            cbBombletRadius = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Bomblet Radius*"), 8f, new ConfigDescription("How large the mini explosions are.")).Value;
            cbBombletProcCoefficient = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Bomblet Proc Coefficient*"), 0.6f, new ConfigDescription("Affects the chance and power of Dynamite Toss Bomblet procs.")).Value;
            ClusterBomb.baseDuration = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Throw Duration"), 0.6f, new ConfigDescription("How long it takes to throw a Dynamite Bundle.")).Value;
            cbCooldown = base.Config.Bind<float>(new ConfigDefinition("Dynamite", "Cooldown"), 6f, new ConfigDescription("How long it takes for Dynamite Toss to recharge.")).Value;
            cbStock = base.Config.Bind<int>(new ConfigDefinition("Dynamite", "Stock"), 1, new ConfigDescription("How much Dynamite you start with.")).Value;
            disableFalloff = base.Config.Bind<bool>(new ConfigDefinition("Dynamite", "Disable Falloff"), false, new ConfigDescription("Disable explosion damage falloff.")).Value;
        }

        public void LoadResources()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BanditDynamite.dynamite"))
            {
                assets = AssetBundle.LoadFromStream(stream);
            }

            using (var bankStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BanditDynamite.BanditDynamite.bnk"))
            {
                var bytes = new byte[bankStream.Length];
                bankStream.Read(bytes, 0, bytes.Length);
                R2API.SoundAPI.SoundBanks.Add(bytes);
            }
        }

        private void SetupClusterBomb()
        {
            ClusterBombDamage = DamageAPI.ReserveDamageType();

            ClusterBombObject = R2API.PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/projectiles/BanditClusterBombSeed"), "MoffeinBanditDynamiteClusterBomb", true);
            R2API.ContentAddition.AddProjectile(ClusterBombObject);

            GameObject ClusterBombGhostObject = R2API.PrefabAPI.InstantiateClone(assets.LoadAsset<GameObject>("DynamiteBundle.prefab"), "MoffeinBanditDynamiteClusterBombGhost", false);
            ClusterBombGhostObject.GetComponentInChildren<MeshRenderer>().material.shader = hotpoo;
            ClusterBombGhostObject.AddComponent<ProjectileGhostController>();

            ClusterBombObject.AddComponent<DynamiteRotation>();

            ClusterBombObject.GetComponent<ProjectileController>().ghostPrefab = ClusterBombGhostObject;

            DamageAPI.ModdedDamageTypeHolderComponent mdc = ClusterBombObject.AddComponent<DamageAPI.ModdedDamageTypeHolderComponent>();
            mdc.Add(ClusterBombDamage);

            float trueBombletDamage = ClusterBomb.bombletDamageCoefficient / ClusterBomb.damageCoefficient;
            SphereCollider sc = ClusterBombObject.AddComponent<SphereCollider>();
            sc.radius = 0.6f;
            sc.contactOffset = 0.01f;

            TeamComponent tc = ClusterBombObject.AddComponent<TeamComponent>();
            tc.hideAllyCardDisplay = true;
            ClusterBombObject.AddComponent<SkillLocator>();

            CharacterBody cb = ClusterBombObject.AddComponent<CharacterBody>();
            cb.rootMotionInMainState = false;
            cb.bodyFlags = CharacterBody.BodyFlags.Masterless;
            cb.baseMaxHealth = 1f;
            cb.baseCrit = 0f;
            cb.baseAcceleration = 0f;
            cb.baseArmor = 0f;
            cb.baseAttackSpeed = 0f;
            cb.baseDamage = 0f;
            cb.baseJumpCount = 0;
            cb.baseJumpPower = 0f;
            cb.baseMoveSpeed = 0f;
            cb.baseMaxShield = 0f;
            cb.baseRegen = 0f;
            cb.autoCalculateLevelStats = true;
            cb.levelArmor = 0f;
            cb.levelAttackSpeed = 0f;
            cb.levelCrit = 0f;
            cb.levelDamage = 0f;
            cb.levelJumpPower = 0f;
            cb.levelMaxHealth = 0f;
            cb.levelMaxShield = 0f;
            cb.levelMoveSpeed = 0f;
            cb.levelRegen = 0f;
            cb.hullClassification = HullClassification.Human;

            HealthComponent hc = ClusterBombObject.AddComponent<HealthComponent>();
            hc.globalDeathEventChanceCoefficient = 0f;
            hc.body = cb;

            ClusterBombObject.AddComponent<AssignDynamiteTeamFilter>();

            //All bombs spawn, but they all spawn inside each other.
            ProjectileImpactExplosion pie = ClusterBombObject.GetComponent<ProjectileImpactExplosion>();
            pie.blastRadius = cbRadius;
            pie.falloffModel = disableFalloff ? BlastAttack.FalloffModel.None : BlastAttack.FalloffModel.SweetSpot;
            pie.lifetime = 25f;
            pie.lifetimeAfterImpact = 1.5f;
            pie.destroyOnEnemy = true;
            pie.destroyOnWorld = false;
            pie.childrenCount = cbBombletCount;
            pie.childrenDamageCoefficient = trueBombletDamage;
            pie.blastProcCoefficient = 1f;
            pie.impactEffect = SetupDynamiteExplosion();
            pie.fireChildren = false;// true;

            pie.explosionSoundString = "";
            pie.lifetimeExpiredSound = null;
            pie.projectileHealthComponent = hc;
            pie.transformSpace = ProjectileImpactExplosion.TransformSpace.World;

            Destroy(ClusterBombObject.GetComponent<ProjectileStickOnImpact>());

            ProjectileSimple ps = ClusterBombObject.GetComponent<ProjectileSimple>();
            ps.velocity = 60f;
            ps.lifetime = 25f;

            ClusterBombObject.GetComponent<Rigidbody>().useGravity = true;

            ProjectileDamage pd = ClusterBombObject.GetComponent<ProjectileDamage>();
            pd.damageType = DamageType.IgniteOnHit;


            AddDynamiteHurtbox(ClusterBombObject);

            ClusterBomb.projectilePrefab = ClusterBombObject;
        }
        private void AddDynamiteHurtbox(GameObject go)
        {
            GameObject hbObject = new GameObject();
            hbObject.transform.parent = go.transform;
            //GameObject hbObject = go;

            hbObject.layer = LayerIndex.entityPrecise.intVal;
            SphereCollider goCollider = hbObject.AddComponent<SphereCollider>();
            goCollider.radius = 0.9f;

            HurtBoxGroup goHurtBoxGroup = hbObject.AddComponent<HurtBoxGroup>();
            HurtBox goHurtBox = hbObject.AddComponent<HurtBox>();
            goHurtBox.isBullseye = false;
            goHurtBox.healthComponent = go.GetComponent<HealthComponent>();
            goHurtBox.damageModifier = HurtBox.DamageModifier.Normal;
            goHurtBox.hurtBoxGroup = goHurtBoxGroup;
            goHurtBox.indexInGroup = 0;

            HurtBox[] goHurtBoxArray = new HurtBox[]
            {
                goHurtBox
            };

            goHurtBoxGroup.bullseyeCount = 0;
            goHurtBoxGroup.hurtBoxes = goHurtBoxArray;
            goHurtBoxGroup.mainHurtBox = goHurtBox;

            DisableCollisionsBetweenColliders dc = go.AddComponent<DisableCollisionsBetweenColliders>();
            dc.collidersA = go.GetComponents<Collider>();
            dc.collidersB = hbObject.GetComponents<Collider>();
        }
        private GameObject SetupDynamiteExplosion()
        {
            GameObject dynamiteExplosion = R2API.PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/effects/omnieffect/omniexplosionvfx"), "MoffeinBanditDynamiteDynamiteExplosion", false);
            ShakeEmitter se = dynamiteExplosion.AddComponent<ShakeEmitter>();
            se.shakeOnStart = true;
            se.duration = 0.5f;
            se.scaleShakeRadiusWithLocalScale = false;
            se.radius = 75f;
            se.wave = new Wave()
            {
                amplitude = 1f,
                cycleOffset = 0f,
                frequency = 40f
            };

            EffectComponent ec = dynamiteExplosion.GetComponent<EffectComponent>();
            ec.soundName = "Play_MoffeinBanditDynamite_explode";

            R2API.ContentAddition.AddEffect(dynamiteExplosion);
            return dynamiteExplosion;
        }
        private void SetupClusterBomblet()
        {
            ClusterBombletObject = R2API.PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/projectiles/BanditClusterGrenadeProjectile"), "MoffeinBanditDynamiteClusterBomblet", true);
            R2API.ContentAddition.AddProjectile(ClusterBombletObject);

            GameObject ClusterBombletGhostObject = R2API.PrefabAPI.InstantiateClone(assets.LoadAsset<GameObject>("DynamiteStick.prefab"), "MoffeinBanditDynamiteClusterBombletGhost", false);
            ClusterBombletGhostObject.GetComponentInChildren<MeshRenderer>().material.shader = hotpoo;
            ClusterBombletGhostObject.AddComponent<ProjectileGhostController>();

            ClusterBombObject.GetComponent<ProjectileImpactExplosion>().childrenProjectilePrefab = ClusterBombletObject;

            ClusterBombletObject.AddComponent<SphereCollider>();
            ClusterBombletObject.GetComponent<ProjectileController>().ghostPrefab = ClusterBombletGhostObject;

            ProjectileImpactExplosion pie = ClusterBombletObject.GetComponent<ProjectileImpactExplosion>();
            pie.blastRadius = cbBombletRadius;
            pie.falloffModel = disableFalloff ? BlastAttack.FalloffModel.None : BlastAttack.FalloffModel.SweetSpot;
            pie.destroyOnEnemy = false;
            pie.destroyOnWorld = false;
            pie.lifetime = 1.5f;
            pie.timerAfterImpact = false;
            pie.blastProcCoefficient = cbBombletProcCoefficient;
            pie.explosionSoundString = "";
            pie.impactEffect = SetupDynamiteBombletExplosion();
            pie.fireChildren = false;

            Destroy(ClusterBombletObject.GetComponent<ProjectileStickOnImpact>());

            ProjectileSimple ps = ClusterBombletObject.GetComponent<ProjectileSimple>();
            ps.velocity = 12f;

            ProjectileDamage pd = ClusterBombletObject.GetComponent<ProjectileDamage>();
            pd.damageType = DamageType.IgniteOnHit;
        }

        private GameObject SetupDynamiteBombletExplosion()
        {
            GameObject dynamiteExplosion = R2API.PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/effects/impacteffects/explosionvfx"), "MoffeinBanditDynamiteBombletExplosion", false);

            EffectComponent ec = dynamiteExplosion.GetComponent<EffectComponent>();
            ec.soundName = "Play_engi_M2_explo";

            R2API.ContentAddition.AddEffect(dynamiteExplosion);
            return dynamiteExplosion;
        }
    }
}
