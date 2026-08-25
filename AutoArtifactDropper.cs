//CODEs GOES TO 2ModInit.cs

//using UnityEngine;

//namespace QualityOfLifeONI
//{
//	public class AutoArtifactDropper : KMonoBehaviour, ISim1000ms
//	{
//		[MyCmpGet]
//		private ArtifactSelector artifactSelector;

//		[MyCmpGet]
//		private RocketModuleCluster rocketModule;

//		private float timer = -1f;
//		private bool wasGrounded = false;

//		protected override void OnSpawn()
//		{
//			base.OnSpawn();
//			wasGrounded = IsGrounded();
//			Subscribe((int)GameHashes.RocketLanded, OnLandedEvent);
//			Subscribe((int)GameHashes.Landed, OnLandedEvent);
//		}

//		private void OnLandedEvent(object data)
//		{
//			if (ModInit.Config != null && ModInit.Config.ArtifactAutoDropEnabled)
//			{
//				timer = 0f;
//			}
//		}

//		public void Sim1000ms(float dt)
//		{
//			if (ModInit.Config == null || !ModInit.Config.ArtifactAutoDropEnabled)
//				return;

//			bool isGrounded = IsGrounded();

//			// Trigger timer on state transition to grounded
//			if (isGrounded && !wasGrounded && timer < 0f)
//			{
//				timer = 0f;
//			}
//			wasGrounded = isGrounded;

//			// Run timer while rocket remains landed
//			if (timer >= 0f && isGrounded)
//			{
//				// Stop timer if there is no artifact stored
//				if (artifactSelector == null || !artifactSelector.HasArtifact())
//				{
//					timer = -1f;
//					return;
//				}

//				timer += dt;
//				float targetDelay = ModInit.Config.ArtifactDropDelaySeconds;

//				if (timer >= targetDelay)
//				{
//					timer = -1f;
//					DropArtifact();
//				}
//			}
//		}

//		private bool IsGrounded()
//		{
//			if (rocketModule != null && rocketModule.CraftInterface != null)
//			{
//				var craft = rocketModule.CraftInterface.GetComponent<Clustercraft>();
//				return craft != null && craft.Status == Clustercraft.CraftStatus.Grounded;
//			}
//			return false;
//		}

//		private void DropArtifact()
//		{
//			if (artifactSelector != null && artifactSelector.HasArtifact())
//			{
//				artifactSelector.DropArtifact();
//			}
//		}
//	}
//}