﻿/*
 * This file is subject to the terms and conditions defined in
 * file 'license.txt', which is part of this source code package.
 */



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace SteamKit2
{
    /// <summary>
    /// This handler handles all interaction with other users on the Steam3 network.
    /// </summary>
    public sealed partial class SteamFriends : ClientMsgHandler
    {
        /// <summary>
        /// The unique name of this hadler.
        /// </summary>
        public const string NAME = "SteamFriends";

        Friend localUser;
        FriendCache cache;


        internal SteamFriends()
            : base( SteamFriends.NAME )
        {
            localUser = new Friend( 0 );
            cache = new FriendCache();
        }


        /// <summary>
        /// Gets the local user's persona name.
        /// </summary>
        /// <returns>The name.</returns>
        public string GetPersonaName()
        {
            return localUser.Name;
        }
        /// <summary>
        /// Sets the local user's persona name and broadcasts it over the network.
        /// </summary>
        /// <param name="name">The name.</param>
        public void SetPersonaName( string name )
        {
            // todo: figure out the structure of this message
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the local user's persona state.
        /// </summary>
        /// <returns>The persona state.</returns>
        public EPersonaState GetPersonaState()
        {
            return localUser.PersonaState;
        }
        /// <summary>
        /// Sets the local user's persona state and broadcasts it over the network.
        /// </summary>
        /// <param name="state">The state.</param>
        public void SetPersonaState( EPersonaState state )
        {
            var stateMsg = new ClientMsg<MsgClientChangeStatus, ExtendedClientMsgHdr>();
            stateMsg.Msg.PersonaState = ( byte )state;

            this.Client.Send( stateMsg );

            // todo: figure out if we get persona state changes for our own actions
            localUser.PersonaState = state;
        }

        /// <summary>
        /// Gets the friend count of the local user.
        /// </summary>
        /// <returns>The number of friends.</returns>
        public int GetFriendCount()
        {
            return cache.GetFriendCount();
        }
        /// <summary>
        /// Gets a friend by index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>A valid steamid of a friend if the index is in range; otherwise a steamid representing 0.</returns>
        public SteamID GetFriendByIndex( int index )
        {
            Friend friend = cache.GetFriendByIndex( index );

            if ( friend == null )
                return 0;

            return friend.SteamID;
        }

        /// <summary>
        /// Gets the persona name of a friend.
        /// </summary>
        /// <param name="steamId">The steam id.</param>
        /// <returns>The name.</returns>
        public string GetFriendPersonaName( SteamID steamId )
        {
            Friend friend = ( steamId == localUser.SteamID ? localUser : cache.GetFriend( steamId ) );

            if ( friend == null || string.IsNullOrEmpty( friend.Name ) )
                return "[unknown]";

            return friend.Name;
        }
        /// <summary>
        /// Gets the persona state of a friend.
        /// </summary>
        /// <param name="steamId">The steam id.</param>
        /// <returns>The persona state</returns>
        public EPersonaState GetFriendPersonaState( SteamID steamId )
        {
            Friend friend = ( steamId == localUser.SteamID ? localUser : cache.GetFriend( steamId ) );

            if ( friend == null )
                return EPersonaState.Offline;

            return friend.PersonaState;
        }
        /// <summary>
        /// Gets the relationship of a friend.
        /// </summary>
        /// <param name="steamId">The steam id.</param>
        /// <returns></returns>
        public EFriendRelationship GetFriendRelationship( SteamID steamId )
        {
            Friend friend = ( steamId == localUser.SteamID ? localUser : cache.GetFriend( steamId ) );

            if ( friend == null )
                return EFriendRelationship.None;

            return friend.Relationship;
        }

        /// <summary>
        /// Gets the game name of a friend playing a game.
        /// </summary>
        /// <param name="steamId">The steam id.</param>
        /// <returns>The game name of a friend playing a game, or null if they haven't been cached yet.</returns>
        public string GetFriendGamePlayedName( SteamID steamId )
        {
            Friend friend = cache.GetFriend( steamId );

            if ( steamId == localUser.SteamID )
                friend = localUser;

            if ( friend == null )
                return null;

            return friend.GameName;
        }
        /// <summary>
        /// Gets the gameid of a friend playing a game.
        /// </summary>
        /// <param name="steamId">The steam id.</param>
        /// <returns>The gameid of a friend playing a game, or 0 if they haven't been cached yet.</returns>
        public GameID GetFriendGamePlayed( SteamID steamId )
        {
            Friend friend = cache.GetFriend( steamId );

            if ( steamId == localUser.SteamID )
                friend = localUser;

            if ( friend == null )
                return 0;

            return friend.GameID;
        }



        /// <summary>
        /// Sends a chat message to a friend.
        /// </summary>
        /// <param name="target">The target to send to.</param>
        /// <param name="type">The type of message to send.</param>
        /// <param name="message">The message to send.</param>
        public void SendChatMessage( SteamID target, EChatEntryType type, string message )
        {
            var chatMsg = new ClientMsgProtobuf<MsgClientFriendMsgProto>();

            chatMsg.Msg.Proto.steamid = target;
            chatMsg.Msg.Proto.chat_entry_type = ( int )type;
            chatMsg.Msg.Proto.message = Encoding.UTF8.GetBytes( message );

            this.Client.Send( chatMsg );
        }


        /// <summary>
        /// Handles a client message. This should not be called directly.
        /// </summary>
        /// <param name="e">The <see cref="SteamKit2.ClientMsgEventArgs"/> instance containing the event data.</param>
        public override void HandleMsg( ClientMsgEventArgs e )
        {
            switch ( e.EMsg )
            {
                case EMsg.ClientPersonaState:
                    HandlePersonaState( e );
                    break;

                case EMsg.ClientFriendsList:
                    HandleFriendsList( e );
                    break;

                case EMsg.ClientFriendMsgIncoming:
                    HandleFriendMsg( e );
                    break;

                case EMsg.ClientAccountInfo:
                    HandleAccountInfo( e );
                    break;
            }
        }



        #region ClientMsg Handlers
        void HandleAccountInfo( ClientMsgEventArgs e )
        {
            var accInfo = new ClientMsgProtobuf<MsgClientAccountInfo>();

            try
            {
                accInfo.SetData( e.Data );
            }
            catch ( Exception ex )
            {
                DebugLog.WriteLine( "SteamFriends", "HandleAccountInfo encountered an exception when trying to read clientmsg.\n{0}", ex.ToString() );
                return;
            }

            localUser.Name = accInfo.Msg.Proto.persona_name;
        }
        void HandleFriendMsg( ClientMsgEventArgs e )
        {
            ClientMsg<MsgClientFriendMsgIncoming, ExtendedClientMsgHdr> friendMsg = null;

            try
            {
                friendMsg = new ClientMsg<MsgClientFriendMsgIncoming, ExtendedClientMsgHdr>( e.Data );
            }
            catch ( Exception ex )
            {
                DebugLog.WriteLine( "SteamFriends", "HandleFriendsMsg encountered an exception when trying to read clientmsg.\n{0}", ex.ToString() );
                return;
            }

            byte[] msgData = friendMsg.Payload.ToArray();

            var callback = new FriendMsgCallback( friendMsg.Msg, msgData );
            this.Client.PostCallback( callback );
        }
        void HandleFriendsList( ClientMsgEventArgs e )
        {
            ClientMsgProtobuf<MsgClientFriendsList> list = null;

            try
            {
                list = new ClientMsgProtobuf<MsgClientFriendsList>( e.Data );
            }
            catch ( Exception ex )
            {
                DebugLog.WriteLine( "SteamFriends", "HandleFriendsList encountered an exception when trying to read clientmsg.\n{0}", ex.ToString() );
                return;
            }

            localUser.SteamID = this.Client.SteamID;

            var reqLocalData = new ClientMsgProtobuf<MsgClientRequestFriendData>();
            reqLocalData.Msg.Proto.persona_state_requested = ( uint )( EClientPersonaStateFlag.PlayerName | EClientPersonaStateFlag.Presence );

            foreach ( var friend in list.Msg.Proto.friends )
            {
                SteamID friendId = new SteamID( friend.ulfriendid );


                if ( friendId.BIndividualAccount() )
                {
                    Friend cacheFriend = new Friend( friendId );
                    cacheFriend.Relationship = ( EFriendRelationship )friend.efriendrelationship;
                    cache.AddFriend( cacheFriend );

                    reqLocalData.Msg.Proto.friends.Add( friend.ulfriendid );
                }

                if ( friendId.BClanAccount() )
                {
                    Clan cacheClan = new Clan( friendId );
                    cache.AddClan( cacheClan );
                }
            }

            this.Client.Send( reqLocalData );

            var callback = new FriendsListCallback( list.Msg.Proto );
            this.Client.PostCallback( callback );
        }
        void HandlePersonaState( ClientMsgEventArgs e )
        {
            ClientMsgProtobuf<MsgClientPersonaState> perState = null;

            try
            {
                perState = new ClientMsgProtobuf<MsgClientPersonaState>( e.Data );
            }
            catch ( Exception ex )
            {
                DebugLog.WriteLine( "SteamFriends", "HandlePersonaState encountered an exception when trying to read clientmsg.\n{0}", ex.ToString() );
                return;
            }

            EClientPersonaStateFlag flags = ( EClientPersonaStateFlag )perState.Msg.Proto.status_flags;

            foreach ( var friend in perState.Msg.Proto.friends )
            {
                Friend cacheFriend = ( friend.friendid == localUser.SteamID ? localUser : cache.GetFriend( friend.friendid ) );

                if ( cacheFriend == null )
                    continue; // persona info was for someone not in our cache

                if ( ( flags & EClientPersonaStateFlag.PlayerName ) == EClientPersonaStateFlag.PlayerName )
                    cacheFriend.Name = friend.player_name;

                if ( ( flags & EClientPersonaStateFlag.Presence ) == EClientPersonaStateFlag.Presence )
                    cacheFriend.PersonaState = ( EPersonaState )friend.persona_state;

                if ( ( flags & EClientPersonaStateFlag.GameExtraInfo ) == EClientPersonaStateFlag.GameExtraInfo )
                {
                    cacheFriend.GameName = friend.game_name;
                    cacheFriend.GameID = friend.gameid;
                    cacheFriend.GameAppID = friend.game_played_app_id;
                }

                var callback = new PersonaStateCallback( friend );
                this.Client.PostCallback( callback );
            }
        }
        #endregion
    }
}