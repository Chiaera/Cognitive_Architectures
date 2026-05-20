using UnityEngine;

namespace Manus.Networking
{
	/// <summary>
	/// A sample Interface which can be used to host or join a game.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu( "Manus/Networking/Simple Interface" )]
	public class SimpleInterface : MonoBehaviour
	{
		/// <summary>
		/// The SimpleNetworkManager that this interface communicates with.
		/// </summary>
		[Tooltip("This is the Network Manager that this interface communicates with.")]
		public SimpleNetworkManager networkManager;

		/// <summary>
		/// The NetLobbyInfo that is used to advertise a lobby if the user decides to be a host.
		/// </summary>
		[Tooltip("Info used to advertise a lobby.")]
		public NetLobbyInfo hostInfo = new NetLobbyInfo();

		/// <summary>
		/// The game object that contains the interface, which is hidden upon connection.
		/// </summary>
		[Tooltip("Contains the connection interface.")]
		public GameObject interfaceGameObject;

		/// <summary>
		/// The SimpleLobbyBrowser that is used to display all the available lobbies.
		/// </summary>
		[Tooltip("Displays all available lobbies.")]
		public SimpleLobbyBrowser lobbyBrowser;

		/// <summary>
		/// This is used for initiating a direct connection (IP).
		/// </summary>
		string m_DirectJoinIPAddress;

		/// <summary>
		/// This is used for initiating a direct connection (Port).
		/// </summary>
		int m_DirectJoinPort;

		/// <summary>
		/// This is used for setting Client ID
		/// </summary>
		int m_ID;

		/// <summary>
		/// This object can be spawned by pressing the G key.
		/// </summary>
		[Tooltip("Spawn this sample object by pression G in play mode.")]
		public NetObject spawnableSample;

		/// <summary>
		/// Called by Unity.
		/// Starts a client for discovery, sets the interface things.
		/// </summary>
		void Start()
		{
			networkManager.Client();
			(networkManager.GetClient() as SimpleClient).lobbyBrowser = lobbyBrowser;
			(networkManager.GetClient() as SimpleClient).simpleInterface = this;
			m_ID = networkManager.GetInstanceID();
		}

		/// <summary>
		/// Called every frame
		/// Checks whether a Client or a Server has pressed on BackQuote "`" 
		/// To spawn the assigned spawnable object in the specified transform.
		/// </summary>
		void Update()
		{
			switch( networkManager.status )
			{
				case NetworkManager.Status.Inactive:
					{
					}
					break;
				case NetworkManager.Status.Client:
					{
						if( Input.GetKeyUp( KeyCode.BackQuote ) )
						{
							networkManager.SpawnObject( spawnableSample, new Vector3( 1, 1, 0 ), Quaternion.identity );
						}
					}
					break;
				case NetworkManager.Status.Server:
					{
						if( Input.GetKeyUp( KeyCode.BackQuote ) )
						{
							networkManager.SpawnObject( spawnableSample, new Vector3( -1, 1, 0 ), Quaternion.identity );
						}
					}
					break;
			}

		}

		/// <summary>
		/// Hosts a server, sets the lobby info and turns off the interface.
		/// </summary>
		public void Host()
		{
			networkManager.GetPeer().Shutdown();
			networkManager.Host();
			networkManager.server.lobbyInfo = hostInfo;
			interfaceGameObject.SetActive( false );
		}

		/// <summary>
		/// Join the lobby selected in the interface.
		/// </summary>
		public void Join()
		{
			var t_SClient = networkManager.GetClient() as SimpleClient;
			if( t_SClient == null ) return;
			t_SClient.stringID = m_ID.ToString();
			var t_Lobby = t_SClient.lobbyBrowser.selectedLobby;
			if( t_Lobby == null ) return;
			networkManager.Connect( t_Lobby.externalHostEndpoint.Address.ToString(), t_Lobby.externalHostEndpoint.Port );
		}

		/// <summary>
		/// Join the first found host.
		/// </summary>
		public void AutoJoin()
		{

			var t_SClient = networkManager.GetClient() as SimpleClient;
			if( t_SClient == null ) return;

			t_SClient.stringID = m_ID.ToString();

			var t_FirstFoundHost = t_SClient.lobbyBrowser.firstFoundHost;
			if( t_FirstFoundHost == null ) return;

			networkManager.Connect( t_FirstFoundHost.externalHostEndpoint.Address.ToString(), t_FirstFoundHost.externalHostEndpoint.Port );
		}

		/// <summary>
		/// Sets the Client Username.
		/// </summary>
		/// <param name="p_Username">Username of the Client</param>
		public void SetClientUsername( string p_Username )
		{
			var t_Client = networkManager.GetClient() as SimpleClient;
			if( t_Client == null ) return;
			t_Client.name = p_Username;
		}

		/// <summary>
		/// Join the lobby via a direct connection
		/// </summary>
		public void DirectJoin()
		{
			networkManager.Connect( m_DirectJoinIPAddress, m_DirectJoinPort );
		}

		/// <summary>
		/// Sets the Hosting Lobby Name.
		/// </summary>
		/// <param name="p_ServerName">Name of the Lobby</param>
		public void SetHostName( string p_ServerName )
		{
			hostInfo.serverName = p_ServerName;
		}

		/// <summary>
		/// Sets the Hosting Lobby Name.
		/// </summary>
		/// <param name="p_UserName">Name of the User</param>
		public void SetUserName( string p_UserName )
		{
			hostInfo.userName = p_UserName;
		}

		/// <summary>
		/// Set the Max Players in the hosting lobby information.
		/// </summary>
		/// <param name="p_Str">Amount of players</param>
		public void SetHostMaxPlayers( string p_Str )
		{
			int t_Cnt;
			if( int.TryParse( p_Str, out t_Cnt ) )
			{
				hostInfo.maxPlayers = (byte)t_Cnt;
			}
		}

		/// <summary>
		/// Sets the Direct Join IP Address
		/// </summary>
		/// <param name="p_Str">IP Address</param>
		public void SetDirectJoinIPAddress( string p_Str )
		{
			m_DirectJoinIPAddress = p_Str;
		}

		/// <summary>
		/// Sets the Direct Join Port
		/// </summary>
		/// <param name="p_Str">Port</param>
		public void SetDirectJoinPort( string p_Str )
		{
			int t_Port;
			if( int.TryParse( p_Str, out t_Port ) )
			{
				m_DirectJoinPort = t_Port;
			}
		}

		/// <summary>
		/// Function called when a connection is made to a server.
		/// </summary>
		public void OnConnected()
		{
			interfaceGameObject.SetActive( false );
		}

		/// <summary>
		/// Function called when a connection is lost to a server.
		/// </summary>
		public void OnDisconnected()
		{
			int t_Idx = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
			UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync( t_Idx );
			Resources.UnloadUnusedAssets();
			UnityEngine.SceneManagement.SceneManager.LoadScene( t_Idx );
		}
	}
}
