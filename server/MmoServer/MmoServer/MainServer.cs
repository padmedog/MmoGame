using System;
using System.IO;
using SharpServer.Buffers;
using SharpServer.Sockets;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GMS_Server
{
    class mainProgram
    {
        /* client received actions
        0 - update entities
        1 - create object
        2 - destroy object
        3 - on player join
        4 - entity created
        5 - entity destroyed
        6 - ping
        7 - got disconnect request
        8 - server wants updated input*/

        //global game variables
        public static SettingsSystem settings;
        public static GameWorld gameWorld;
        public static TcpServerHandler game_server;
        public static ulong gameSteps;
        public const string CONSOLE_FILE_PATH = @".\console.txt";
        public static Queue<string> ConsoleQueue;
        public static void Main()
        {
            //make the socketbinder
            SocketBinder binder = new SocketBinder();
            //make the console queue
            ConsoleQueue = new Queue<string>();
            InitConsoleFile();
            //make the settings
            settings = new SettingsSystem();
            //we need to load from the file, if there is one
            settings.ReadFile();
            //lets make the server too
            game_server = new TcpServerHandler(binder, settings.port, settings.maxConnections, settings.alignment, settings.timeout, settings.address);
            //start the server
            game_server.Start();
            //now lets set the methods for event stuff
            game_server.StartedEvent += event_started;
            game_server.ClientCreatedEvent += event_clientCreated;
            game_server.ClosedEvent += event_closed;
            game_server.ReceivedEvent += event_received;
            game_server.ConnectedEvent += event_connected;
            game_server.DisconnectedEvent += event_disconnected;
            game_server.AttemptReconnectEvent += event_attemptReconnect;
            game_server.ClientOverflowEvent += event_clientOverflow;
            //now for the game world
            gameWorld = new GameWorld(game_server);

            //and now for the main game loop
            int sleepJump = 0,
                times = 0;
            gameSteps = 0;
            //anything after here is not accessible until the server ends
            while (game_server.Status)
            {
                //commands
                CommandSystem.DoCommand(CommandSystem.Update());

                if (sleepJump == 2)
                {
                    sleepJump = 0;
                    event_step();
                    times++;
                    Thread.Sleep(16);
                }
                else
                {
                    sleepJump++;
                    event_step();
                    times++;
                    Thread.Sleep(17);
                }
                sleepJump++;
            }//whatever after the loop is triggered here when the server is closed
            UpdateConsoleFile();
            Thread.Sleep(1000);
        }

        private static void event_clientOverflow(TcpClientHandler client)
        {
            mainProgram.WriteLine("Connection attempt from " + getIp(client).ToString() + ", socket " + client.Socket.ToString() + ", however the server is full");
        }

        private static void event_attemptReconnect(TcpClientHandler client)
        {
            mainProgram.WriteLine("Client " + client.Socket.ToString() + "'s connection is unknown and unknowably unrecoverable (ip " + getIp(client).ToString() + ")");
            //no code ahead because I have no idea how to reconnect a client
        }

        private static void event_disconnected(TcpClientHandler client)
        {
            mainProgram.WriteLine("Client " + client.Socket.ToString() + " disconnected from " + getIp(client).ToString());
            int id_ = gameWorld.getPlayer(client.Socket);
            gameWorld.removeClient(id_);
        }

        private static void event_connected(TcpClientHandler client)
        {
            mainProgram.WriteLine("Client connected from " + getIp(client).ToString());
            //we'll put our code to initiate the player client here
            int entid_ = gameWorld.createPlayer(new GamePoint3D(0d, 0d, 512d), client);
            int sz_ = 16 + (gameWorld.entityMap.Count * 52) + (gameWorld.objectMap.Count * 52);
            BufferStream buff = new BufferStream(sz_, 1);
            buff.Write((ushort)3);
            buff.Write(entid_);
            buff.Write((uint)gameWorld.entityMap.Count);
            foreach (KeyValuePair<int, GameEntity> pair in gameWorld.entityMap)
            {
                buff.Write(pair.Value.id);
                buff.Write(pair.Value.pos.X);
                buff.Write(pair.Value.pos.Y);
                buff.Write(pair.Value.pos.Z);
                buff.Write(pair.Value.size.X);
                buff.Write(pair.Value.size.Y);
                buff.Write(pair.Value.direction);
                buff.Write(pair.Value.pitch);
            }
            buff.Write((uint)gameWorld.objectMap.Count);
            foreach (KeyValuePair<int, GameObject> pair in gameWorld.objectMap)
            {
                buff.Write(pair.Value.id);
                buff.Write(pair.Value.position.X);
                buff.Write(pair.Value.position.Y);
                buff.Write(pair.Value.position.Z);
                buff.Write(pair.Value.size.X);
                buff.Write(pair.Value.size.Y);
                buff.Write(pair.Value.size.Z);
            }
            gameWorld.sendToClient(client, buff);
            buff.Deallocate();
            mainProgram.WriteLine("World and client data sent to socket " + client.Socket.ToString());
        }

        private static void event_received(TcpClientHandler client, BufferStream readBuffer)
        {
            int plid = gameWorld.getPlayer(client.Socket);

            //this following line is probably absolutely essential to deal with the GMS packets
            readBuffer.Seek(12);
            BufferStream buff_ = null;
            //get the message id, so we know what to do with the packet
            ushort msgid; readBuffer.Read(out msgid);
            switch (msgid)
            {
                case 0: //client controls update
                    /* so how this works, is that the control info is sent in a single byte, where each bit represents a movement or something, seen below
                        0000000X - forward
                        000000X0 - backward
                        00000X00 - strafe left
                        0000X000 - strafe right
                        000X0000 - up ~jump
                        00X00000 - down ~crouch
                    */
                    byte in_; readBuffer.Read(out in_);
                    bool[] actions = GameGeometry.parse_binary(in_);
                    if (plid >= 0)
                    {
                        gameWorld.clientMap[plid].inputMap.setInput("forward", actions[0]);
                        gameWorld.clientMap[plid].inputMap.setInput("backward", actions[1]);
                        gameWorld.clientMap[plid].inputMap.setInput("left", actions[2]);
                        gameWorld.clientMap[plid].inputMap.setInput("right", actions[3]);
                        gameWorld.clientMap[plid].inputMap.setInput("up", actions[4]);
                        gameWorld.clientMap[plid].inputMap.setInput("down", actions[5]);
                    }
                    break;
                case 1: //client view update
                    //we should be getting two floats, one for direction and one for pitch
                    float dir_; readBuffer.Read(out dir_);
                    float pit_; readBuffer.Read(out pit_);
                    if (plid >= 0)
                    {
                        gameWorld.clientMap[plid].inputMap.setInput("view_x", dir_);
                        gameWorld.clientMap[plid].inputMap.setInput("view_y", pit_);
                    }
                    break;
                case 2: //client sent back a ping
                    GameClient client_ = gameWorld.getClientFromSocket(client.Socket);
                    client_.pingWatch.Stop();
                    mainProgram.WriteLine("Socket " + client.Socket.ToString() + " ping is " + client_.pingWatch.ElapsedMilliseconds);
                    break;
                case 3: //client is disconnecting
                    buff_ = new BufferStream(2, 1);
                    buff_.Write((ushort)7);
                    gameWorld.sendToClient(client, buff_);
                    client.Connected = false;
                    break;
                case 4: //client requested an entity
                    int entId; readBuffer.Read(out entId);
                    buff_ = new BufferStream(64, 1);
                    buff_.Write((ushort)4);
                    buff_.Write(gameWorld.entityMap[entId].id);
                    buff_.Write(gameWorld.entityMap[entId].pos.X);
                    buff_.Write(gameWorld.entityMap[entId].pos.Y);
                    buff_.Write(gameWorld.entityMap[entId].pos.Z);
                    buff_.Write(gameWorld.entityMap[entId].size.X);
                    buff_.Write(gameWorld.entityMap[entId].size.Y);
                    buff_.Write(gameWorld.entityMap[entId].direction);
                    buff_.Write(gameWorld.entityMap[entId].pitch);
                    gameWorld.sendToClient(client, buff_);
                    break;
                default:
                    mainProgram.WriteLine("invalid packet received");
                    break;
            }
            if (buff_ != null)
                buff_.Deallocate();
        }

        private static void event_closed(TcpServerHandler host)
        {
            if (host.ClientMap.Count > 0)
            {
                mainProgram.WriteLine("Closing the server with " + host.ClientMap.Count.ToString() + " clients");
            }
            else
            {
                mainProgram.WriteLine("Closing the server with no clients");
            }
        }

        private static TcpClientHandler event_clientCreated(SocketBinder binder, TcpServerHandler server, uint clientTimeout)
        {
            TcpClientHandler tmp_ = new TcpClientHandler(binder, server, clientTimeout);
            mainProgram.WriteLine("Client " + tmp_.Socket.ToString() + " has been created");
            return tmp_;
        }

        private static void event_started(TcpServerHandler host)
        {
            mainProgram.WriteLine("Server is now running");
        }

        private static async void event_step()
        {
            gameSteps++;
            if (game_server.ClientMap.Count > 0)
                await Task.Run(() =>
                {
                    gameWorld.update();
                });
        }

        public static IPAddress getIp(TcpClientHandler client)
        {
            return getIPEndPoint(client).Address;
        }
        public static int getPort(TcpClientHandler client)
        {
            return getIPEndPoint(client).Port;
        }
        public static IPEndPoint getIPEndPoint(TcpClientHandler client)
        {
            return (IPEndPoint)client.Receiver.Client.RemoteEndPoint;
        }
        private static void InitConsoleFile()
        {
            StreamWriter sw = new StreamWriter(CONSOLE_FILE_PATH, false);
            sw.WriteLine("Console file output started");
            sw.Close();
        }
        public static void UpdateConsoleFile()
        {
            StreamWriter sw = new StreamWriter(CONSOLE_FILE_PATH, true);
            while(ConsoleQueue.Count > 0)
            {
                sw.WriteLine(ConsoleQueue.Dequeue());
            }
            sw.Close();
        }
        public static void WriteLine(object obj)
        {
            WriteLine(obj.ToString());
        }
        public static void WriteLine(string format, object obj)
        {
            WriteLine(string.Format(format, obj));
        }
        public static void WriteLine(string format, params object[] objs)
        {
            WriteLine(string.Format(format, objs));
        }
        public static void WriteLine(string text)
        {
            DateTime now = DateTime.Now;
            text = string.Format("{0}:{1}:{2} - {3}", now.Hour, now.Minute, now.Second, text);
            ConsoleQueue.Enqueue(text);

            ClearNextConsoleLine();
            Console.WriteLine(text);
            CommandSystem.DoCommand(CommandSystem.Update());
        }
        public static void ClearNextConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop + 1);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
    }
}