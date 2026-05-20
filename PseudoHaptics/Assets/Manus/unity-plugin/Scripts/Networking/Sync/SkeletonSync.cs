
using LidNet = Lidgren.Network;

namespace Manus.Networking.Sync
{
	/// <summary>
	/// This is syncs the necessary Hand information.
	/// </summary>
	[UnityEngine.DisallowMultipleComponent]
	[UnityEngine.AddComponentMenu( "Manus/Networking/Sync/Skeleton (Sync)" )]

	public class SkeletonSync : BaseSync
	{
		private Skeletons.Skeleton m_Skeleton;

		private bool m_NewData;

		/// <summary>
		/// The function called when a NetObject is Initialized.
		/// </summary>
		/// <param name="p_Object">The Net Object this Sync belongs to.</param>
		public override void Initialize( Manus.Networking.NetObject p_Object )
		{
			m_Skeleton = GetComponent<Skeletons.Skeleton>();
			m_Skeleton.isLocalPlayer = false;
			m_Skeleton.onDataApplied += () => { m_NewData = true; };
		}

		/// <summary>
		/// The function called when a Syncable needs to be cleaned.
		/// This function should make the IsDirty return false.
		/// </summary>
		public override void Clean()
		{
			m_NewData = false;
		}

		/// <summary>
		/// The function called to see if a Syncable is dirty.
		/// Returns true if it need to be Synced.
		/// </summary>
		/// <returns>Returns true if it need to be Synced.</returns>
		public override bool IsDirty()
		{
			if( !m_Skeleton ) return false;

			return m_NewData;
		}

		/// <summary>
		/// Writes all information that needs to be synced.
		/// </summary>
		/// <param name="p_Msg">The buffer to write the data to</param>
		public override void WriteData( LidNet.NetBuffer p_Msg )
		{
			//if( m_Skeleton == null ) m_Skeleton = GetComponent<Skeletons.Skeleton>();

			foreach( var t_Node in m_Skeleton.skeletonData.nodes )
			{
				p_Msg.Write( t_Node.unityTransform.position );
				p_Msg.Write( t_Node.unityTransform.rotation );
				p_Msg.Write( t_Node.unityTransform.localScale );
			}
		}

		/// <summary>
		/// Receives all information that needs to be synced.
		/// </summary>
		/// <param name="p_Msg">The buffer to read the data from</param>
		public override void ReceiveData( LidNet.NetBuffer p_Msg )
		{
			//if( m_Skeleton == null ) m_Skeleton = GetComponent<Skeletons.Skeleton>();

			foreach( var t_Node in m_Skeleton.skeletonData.nodes )
			{
				t_Node.unityTransform.position = p_Msg.ReadVector3();
				t_Node.unityTransform.rotation = p_Msg.ReadQuaternion();
				t_Node.unityTransform.localScale = p_Msg.ReadVector3();
			}
		}

		/// <summary>
		/// Called when this game instance gets control of the NetObject.
		/// </summary>
		/// <param name="p_Object">The NetObject this game instance gets control of.</param>
		public override void OnGainOwnership( NetObject p_Object )
		{
			m_Skeleton.isLocalPlayer = true;
			m_Skeleton.enabled = true;
		}

		/// <summary>
		/// Called when this game instance loses control of the NetObject.
		/// </summary>
		/// <param name="p_Object">The NetObject this game instance loses control of.</param>
		public override void OnLoseOwnership( NetObject p_Object )
		{
			m_Skeleton.isLocalPlayer = false;
			m_Skeleton.enabled = false;
		}
	}
}


