using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace BanditDynamite.Components
{
    public class BanditNetworkCommands : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcResetSpecialCooldown()
        {
            if (this.hasAuthority)
            {
                skillLocator.special.stock = skillLocator.special.maxStock;
                skillLocator.special.rechargeStopwatch = 0f;
            }
        }

        private void Awake()
        {
            characterBody = base.GetComponent<CharacterBody>();
            skillLocator = base.GetComponent<SkillLocator>();
        }

        private SkillLocator skillLocator;
        private CharacterBody characterBody;
    }
}
