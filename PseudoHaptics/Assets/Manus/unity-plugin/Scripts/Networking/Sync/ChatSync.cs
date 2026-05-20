using UnityEngine;
using UnityEngine.UI;

using LidNet = Lidgren.Network;

namespace Manus.Networking.Sync
{
	[RequireComponent( typeof( Canvas ) )]
	public class ChatSync : BaseSync
	{
		[Tooltip("The text field that is used to display the chat text.")]
		public Text chatOutputField;
		[Tooltip("The input field that is used to send text to others.")]
		public InputField chatInputField;
		[Tooltip("The button used to open and close the chat object.")]
		public Button chatButton;
		[Tooltip("The text object that is modified depending on if the chat is open.")]
		public Text buttonText;
		[Tooltip("The panel that contains the chat window.")]
		public GameObject panel;

		private Canvas m_ChatCanvas;
		private string m_ChatInputText;

		/// <summary>
		/// The function called when a NetObject is Initialized.
		/// </summary>
		/// <param name="p_Object">The Net Object this Sync belongs to.</param>
		public override void Initialize( NetObject p_Object )
		{
			m_ChatCanvas = GetComponent<Canvas>();
			chatButton.onClick.AddListener( TaskOnClick );
		}


		public void Update()
		{
			//Check and set canvas's camera to the player's camera to control the chat screen
			if( m_ChatCanvas.worldCamera == null )
			{
				m_ChatCanvas.worldCamera = Camera.main;
				chatInputField.Select();
				chatInputField.ActivateInputField();
			}
			//close or open the chat screen
			if( !panel.activeSelf )
			{
				chatButton.image.color = Color.green;
				buttonText.text = "Chat";
			}
			else
			{
				chatButton.image.color = Color.red;
				buttonText.text = "X";
			}
		}
		/// <summary>
		/// Button with on and off function
		/// </summary>
		void TaskOnClick()
		{
			if( panel.activeSelf )
			{
				panel.SetActive( false );

			}
			else
			{
				panel.SetActive( true );

			}
		}

		/// <summary>
		/// The function called when a Syncable needs to be cleaned.
		/// This function should make the IsDirty return false.
		/// </summary>
		public override void Clean()
		{
			string t_Text = "";
			if( chatOutputField != null ) t_Text = chatOutputField.text;
			m_ChatInputText = t_Text;
		}

		/// <summary>
		/// The function called to see if a Syncable is dirty.
		/// Returns true if it need to be Synced.
		/// </summary>
		/// <returns>Returns true if it need to be Synced.</returns>
		public override bool IsDirty()
		{
			if( chatOutputField.text != m_ChatInputText ) return true;
			return false;
		}

		/// <summary>
		/// Writes all information that needs to be synced.
		/// </summary>
		/// <param name="p_Msg">The buffer to write the data to</param>
		public override void WriteData( LidNet.NetBuffer p_Msg )
		{
			string t_Text = chatOutputField.text;
			p_Msg.Write( t_Text );
		}

		/// <summary>
		/// Receives all information that needs to be synced.
		/// </summary>
		/// <param name="p_Msg">The buffer to read the data from</param>
		public override void ReceiveData( LidNet.NetBuffer p_Msg )
		{
			chatOutputField.text = p_Msg.ReadString();
		}

		/// <summary>
		/// Called when this game instance gets control of the NetObject.
		/// </summary>
		/// <param name="p_Object">The NetObject this game instance gets control of.</param>
		public override void OnGainOwnership( NetObject p_Object )
		{
			SimpleServer t_Server = NetworkManager.instance.GetServer() as SimpleServer;

			if( t_Server != null )
			{
				t_Server.chatText = chatOutputField;
			}
		}

		/// <summary>
		/// Called when this game instance loses control of the NetObject.
		/// </summary>
		/// <param name="p_Object">The NetObject this game instance loses control of.</param>
		public override void OnLoseOwnership( NetObject p_Object )
		{
		}

		/// <summary>
		/// Called when a text needs to be sent to the chat screen
		/// </summary>
		/// <param name="p_Msg"></param>
		public void SendChatMessage( string p_Msg )
		{
			if( !string.IsNullOrWhiteSpace( p_Msg ) )
			{
				SimpleClient t_Client = NetworkManager.instance.GetClient() as SimpleClient;

				if( t_Client != null )
				{
					LidNet.NetBuffer t_Buff = new LidNet.NetBuffer();
					t_Buff.Write( p_Msg );
					t_Client.SendMessage( Manus.Networking.Message.Type.ChatTextSend, t_Buff );
				}
				else
				{
					SimpleServer t_Server = NetworkManager.instance.GetServer() as SimpleServer;

					string t_HostName;

					if( string.IsNullOrEmpty( t_Server.lobbyInfo.userName ) )
					{
						t_HostName = "Host";
					}
					else
					{
						t_HostName = t_Server.lobbyInfo.userName;
					}
					chatOutputField.text += $"\n{t_HostName}~" + p_Msg;
				}
			}
		}

	}
}
