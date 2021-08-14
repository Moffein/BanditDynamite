using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RoR2;
using UnityEngine.Networking;

namespace BanditDynamite.Components
{
	public class AssignDynamiteTeamFilter : MonoBehaviour
	{
		// Token: 0x060025A0 RID: 9632 RVA: 0x000B00CC File Offset: 0x000AE2CC
		private void Start()
		{
			if (NetworkServer.active)
			{
				TeamComponent teamComponent = base.GetComponent<TeamComponent>();
				/*TeamFilter teamFilter = base.GetComponent<TeamFilter>();
				if (teamFilter && teamComponent)
				{
					teamComponent.teamIndex = teamFilter.teamIndex;
				}*/
				teamComponent.teamIndex = TeamIndex.Neutral;
			}
		}
		public bool fired = false;
	}
}
