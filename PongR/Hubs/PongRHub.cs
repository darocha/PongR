﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using PongR.Models;
using SignalR.Hubs;
using System.Threading.Tasks;
using System.Dynamic;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;

namespace PongR.Hubs
{
    public class PongRHub : Hub, IDisconnect
    {
        private InMemoryUserRepository _userRepository;
        private InMemoryRoomRepository _roomRepository;

        public PongRHub()
        {
            _userRepository = InMemoryUserRepository.GetInstance();
            _roomRepository = InMemoryRoomRepository.GetInstance();
        }

        #region IDisconnect event handler
        /// <summary>
        /// Fired when a user disconnects. 
        /// </summary>
        /// <returns></returns>
        public Task Disconnect()
        {
            // 1: Get the user that disconnected
            // 2: Remove him from the list of connected users
            // 3: If the user was playing, notify the opponent that the user disconnected
            // 4: Re-queue the opponent in the waiting list
            // 5: Remove the room from the list
            User user = _userRepository.ConnectedUsers.Where(u => u.Id.Equals(Context.ConnectionId)).FirstOrDefault();
            if (user != null)
            {
                _userRepository.RemoveUser(user);
                PlayRoom room = _roomRepository.Rooms.Where(r => (r.Player1.Id.Equals(user.Id) || r.Player2.Id.Equals(user.Id))).FirstOrDefault();
                // if the user was in the middle of a match
                if (room != null)
                {                    
                    var opponent = room.Player1.Id.Equals(user.Id) ? room.Player2 : room.Player1;
                    _userRepository.AddToWaitingList(opponent);
                    _roomRepository.Remove(room);
                    return Clients[opponent.Id].opponentLeft();                    
                }
            }
            return null;
        }
        #endregion

        #region PongR event handlers
        /// <summary>
        /// Invoked when a new client joins the system
        /// </summary>        
        public Task Joined()
        {
            Random random = new Random();            
            // 1: Add user to list of connected users
            // 2: If waiting list is empty add user to waiting list            
            // 3: Else find an opponent (first in the waiting list) and remove him from the waiting list
            // 4: Create room and assign both users
            // 5: Create a group for this room
            // 6: Setup match (playRoom Id, initial ball direction, player on the left and right etc...)
            // 6: Notify the group the match can start
            var user = new User()
            {
                Id = Context.ConnectionId,
                Username = Caller.username
            };
            _userRepository.AddUser(user);
            if (_userRepository.WaitingList.Count() == 0)
            {
                _userRepository.AddToWaitingList(user);
                return Caller.wait();
            }
            else
            {
                var opponent = _userRepository.WaitingList.First();
                _userRepository.RemoveFromWaitingList(opponent);
                var playRoom = new PlayRoom()
                {
                    Id = Guid.NewGuid().ToString(),
                    Player1 = opponent,
                    Player2 = user
                };
                _roomRepository.Add(playRoom);
                Task t1 = Groups.Add(opponent.Id, playRoom.Id);
                Task t2 = Groups.Add(user.Id, playRoom.Id);

                t1.Wait();
                t2.Wait();
                // Rough solution. We have to be sure the clients have received the group add messages over the wire
                // TODO: ask maybe on Jabbr or on StackOverflow and think about a better solution
                for (int i = 0; i < 1000000; i++)
                {
                    int z = i * 4;
                }

                dynamic matchOptions = new ExpandoObject();
                matchOptions.PlayRoomId = playRoom.Id;
                matchOptions.Player1 = playRoom.Player1;
                matchOptions.Player2 = playRoom.Player2;
                matchOptions.BallDirection = random.Next() % 2 == 0 ? "left" : "right";
                var list = Clients[playRoom.Id];
                return Clients[playRoom.Id].startMatch(matchOptions);                               
            }
        }        

        public Task NotifyPosition(string playRoomId, string player)
        {
            User playerToNotify = null;            
            dynamic opponent = JObject.Parse(player);
            var playRoom = _roomRepository.Rooms.Where(r => r.Id.Equals(playRoomId)).FirstOrDefault();
            if (playRoom != null)
            {
                playerToNotify = opponent.playerNumber == 1 ? playRoom.Player2 : playRoom.Player1;
                return Clients[playerToNotify.Id].updatePosition(opponent);
            }
            return null;
        }
        #endregion
    }
}

