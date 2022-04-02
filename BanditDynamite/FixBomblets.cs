using UnityEngine;
using RoR2;
using RoR2.Projectile;
using UnityEngine.Networking;
using static R2API.DamageAPI;

//Credits to Dragonyck for this fix
namespace BanditDynamite
{
    public class FireBomblets
    {
        static int[] bomblets = new int[BanditDynamite.cbBombletCount];
        static Quaternion GetRandomDirectionForChild(ProjectileExplosion self)
        {
            Quaternion lhs = Quaternion.AngleAxis(RoR2Application.rng.nextInt + UnityEngine.Random.Range(0f, RoR2Application.rng.nextInt), Vector3.forward);
            Quaternion rhs = Quaternion.AngleAxis(RoR2Application.rng.nextInt + UnityEngine.Random.Range(0f, RoR2Application.rng.nextInt), Vector3.left);
            Quaternion randomChildRollPitch = lhs * rhs;
            if (self)
            {
                return self.transform.rotation * randomChildRollPitch;
            }
            return randomChildRollPitch;
        }
        static void FireBomblet(ProjectileExplosion self)
        {
            Quaternion randomDirectionForChild = GetRandomDirectionForChild(self);
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(BanditDynamite.ClusterBombletObject, self.transform.position, randomDirectionForChild);
            ProjectileController component = gameObject.GetComponent<ProjectileController>();
            if (component)
            {
                component.procChainMask = self.projectileController.procChainMask;
                component.procCoefficient = self.projectileController.procCoefficient;
                component.Networkowner = self.projectileController.owner;
            }
            gameObject.GetComponent<TeamFilter>().teamIndex = self.GetComponent<TeamFilter>().teamIndex;
            ProjectileDamage component2 = gameObject.GetComponent<ProjectileDamage>();
            if (component2)
            {
                component2.damage = self.projectileDamage.damage * EntityStates.Moffein.BanditDynamite.ClusterBomb.bombletDamageCoefficient / EntityStates.Moffein.BanditDynamite.ClusterBomb.damageCoefficient;
                component2.crit = self.projectileDamage.crit;
                component2.force = self.projectileDamage.force;
                component2.damageColorIndex = self.projectileDamage.damageColorIndex;
            }
            NetworkServer.Spawn(gameObject);
        }
        public static void AddHook()
        {
            On.RoR2.Projectile.ProjectileExplosion.DetonateServer += (orig, self) =>
            {
                orig(self);
                if (self)
                {
                    if (self.gameObject)
                    {
                        var c = self.gameObject.GetComponent<ModdedDamageTypeHolderComponent>();
                        if (c)
                        {
                            if (c.Has(BanditDynamite.ClusterBombDamage))
                            {
                                if (NetworkServer.active)
                                {
                                    foreach (int i in bomblets)
                                    {
                                        FireBomblet(self);
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
