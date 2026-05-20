using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace Manus.Networking
{
	/// <summary>
	/// A sample Lobby Browser to display how one might want to show lobbies.
	/// </summary>
	[AddComponentMenu( "Manus/Networking/Simple Lobby Browser" )]
	public class SimpleLobbyBrowser : MonoBehaviour
	{
		[Tooltip("The network manager this lobby browser belongs to.")]
		public NetworkManager manager;

		[Tooltip("The button of of which all lobby buttons are generated.")]
		public GameObject genericLobbyGameButton;
		[Tooltip("The object that is used to display the UI for entering the client's username.")]
		public GameObject clientUsernameObject;
		[Tooltip("The button to join a lobby.")]
		public Button joinButton;
		[Tooltip("The Rect Transform in which the lobby list is generated.")]
		public RectTransform lobbyGameListRect;

		[Tooltip("When enabled the lobby's interface will update.")]
		public bool shouldUpdate = false;
		[Tooltip("The first host that has been found.")]
		public NetLobbyInfo firstFoundHost;
		private float m_Timer = 0;


		/// <summary>
		/// The currently selected Lobby
		/// </summary>
		public NetLobbyInfo selectedLobby
		{
			get
			{
				return m_SelectedLobby;
			}
		}
		public List<NetLobbyInfo> lobbyList
		{
			get
			{
				return m_LobbyList;
			}
		}

		List<NetLobbyInfo> m_LobbyList = null;
		List<Button> m_LobbyButtons = new List<Button>();
		NetLobbyInfo m_SelectedLobby = null;

		/// <summary>
		/// The update function checks if the lobby should be updated
		/// </summary>
		private void Update()
		{

			m_Timer += Time.deltaTime;

			if( m_Timer >= 10 )
			{
				m_Timer = 0;

				var t_FindHosts = FindAvailableHosts();

				if( t_FindHosts.Count > 0 ) firstFoundHost = t_FindHosts[0];
			}

			if( shouldUpdate )
			{
				shouldUpdate = false;
				UpdateBrowser( FindAvailableHosts() );
			}

			if( m_LobbyList.Count > 0 && m_SelectedLobby != null )
			{
				if( !clientUsernameObject.activeSelf )
				{
					clientUsernameObject.SetActive( true );
				}
			}
			else
			{
				clientUsernameObject.SetActive( false );
			}
		}

		/// <summary>
		/// Fetch available hosts on the network which can be connected to.
		/// </summary>
		/// <returns></returns>
		public List<NetLobbyInfo> FindAvailableHosts()
		{
			Client t_Client = manager.GetClient();

			List<NetLobbyInfo> t_Info = new List<NetLobbyInfo>();

			if( t_Client != null )
			{
				var t_DS = t_Client.discoveredServers;

				if( t_DS.Count < 1 ) manager.DiscoverLan();
				if( t_DS.Count > 0 )
				{
					foreach( var t_Serv in t_DS )
					{
						t_Info.Add( t_Serv.Value as NetLobbyInfo );
					}
				}
			}
			return t_Info;
		}

		/// <summary>
		/// Updates the list of lobbies in the interface.
		/// </summary>
		/// <param name="p_LobbyList">List of lobbies</param>
		public void UpdateBrowser( List<NetLobbyInfo> p_LobbyList )
		{
			m_LobbyList = p_LobbyList;
			GameObject t_LGObj;
			for( int i = 0; i < lobbyGameListRect.childCount; i++ )
			{
				t_LGObj = lobbyGameListRect.GetChild( i ).gameObject;
				if( t_LGObj && t_LGObj.gameObject.activeSelf ) Destroy( t_LGObj );
			}

			m_LobbyButtons.Clear();
			for( int i = 0; i < m_LobbyList.Count; i++ )
			{
				firstFoundHost = m_LobbyList[0];
				var t_GObj = Instantiate(genericLobbyGameButton);
				t_GObj.SetActive( true );
				var t_RTrans = t_GObj.GetComponent<RectTransform>();
				t_RTrans.SetParent( lobbyGameListRect );
				t_RTrans.localPosition = Vector3.zero;
				t_RTrans.localScale = Vector3.one;

				var t_Button = t_GObj.GetComponent<Button>();
				t_Button.onClick = new Button.ButtonClickedEvent();
				m_LobbyButtons.Add( t_Button );

				var t_Item = m_LobbyList[i];
				long t_HostKey = m_LobbyList[i].hostID;
				t_Button.onClick.AddListener( delegate ()
				 {
					 for( int b = 0; b < m_LobbyButtons.Count; b++ )
					 {
						 var t_DeselectedButton = m_LobbyButtons[b].colors;
						 t_DeselectedButton.normalColor = Color.white;

						 m_LobbyButtons[b].colors = t_DeselectedButton;
					 }
					 var t_Buttons =  t_GObj.GetComponent<Button>();

					 var t_SelectedButtonColor = t_Buttons.colors;
					 t_SelectedButtonColor.normalColor = Color.gray;
					 t_SelectedButtonColor.highlightedColor = Color.gray;
					 t_Buttons.colors = t_SelectedButtonColor;

					 m_SelectedLobby = t_Item;
				 } );

				var t_Text = t_RTrans.GetComponentInChildren<Text>();
				t_Text.text = m_LobbyList[i].externalHostEndpoint.Address.ToString();

				if( !m_LobbyList.Contains( t_Item ) )
				{
					Destroy( t_Button );
				}
			}

		}
	}
}
