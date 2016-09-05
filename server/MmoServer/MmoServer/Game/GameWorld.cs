using System;
using System.Diagnostics;
using System.Collections.Generic;
using SharpServer.Sockets;
using SharpServer.Buffers;

namespace GMS_Server
{
    public class GameWorld
    {
        public Dictionary<int, GameEntity> entityMap { get; private set; }
        public Dictionary<int, GameClient> clientMap;
        public Dictionary<int, GameObject> objectMap;
        private TcpServerHandler gameServer;
        public int maxEntities, maxObjects;
        public GameWorld(TcpServerHandler _gameServer)
        {
            gameServer = _gameServer;
            maxEntities = 32;
            maxObjects = 64;
            entityMap = new Dictionary<int, GameEntity>();
            clientMap = new Dictionary<int, GameClient>(gameServer.MaxConnections);
            objectMap = new Dictionary<int, GameObject>();
            createObject(new GamePoint3D(0d, 0d, -32d), new GamePoint3D(1024d, 1024d, 32d));
            createObject(new GamePoint3D(256d, 256d, 128d), new GamePoint3D(512d, 512d, 32d));
        }
        public bool createEntity(GamePoint3D position)
        {
            return createEntity(position, new GamePoint2D(16d,64d));
        }
        public bool createEntity(GamePoint3D position, GamePoint2D size)
        {
            //returns if successful
            if (entityMap.Count < maxEntities)
                for(int i = 0; i < maxEntities; i += 1)
                    if(!entityMap.ContainsKey(i))
                    {
                        entityMap.Add(i, new GameEntity(position, size, i, this));
                        return true;
                    }
            return false;
        }
        public bool removeEntity(int Id)
        {
            //returns if successful
            if(entityMap.ContainsKey(Id))
            {
                entityMap.Remove(Id);

                BufferStream buff = new BufferStream(8, 1);
                buff.Write((ushort)5);
                buff.Write((uint)Id); //InvalidOperationException
                sendToAllClients(buff);
                buff.Deallocate();
                return true;
            }
            return false;
        }
        public bool createObject(GamePoint3D position, GamePoint3D size)
        {
            if(objectMap.Count < maxObjects)
                for(int i = 0; i < maxObjects; i += 1)
                    if(!objectMap.ContainsKey(i))
                    {
                        objectMap.Add(i, new GameObject(position, size, i, this));
                        return true;
                    }
            return false;
        }
        public bool removeObject(int id)
        {
            if(objectMap.ContainsKey(id))
            {
                objectMap.Remove(id);

                BufferStream buff = new BufferStream(1024, 1);
                buff.Write((ushort)2);
                buff.Write((uint)id);
                sendToAllClients(buff);
                buff.Deallocate();
                return true;
            }
            return false;
        }
        public int createPlayer(GamePoint3D position, TcpClientHandler client)
        {
            return createPlayer(position, new GamePoint2D(16d, 64d), client);
        }
        public int createPlayer(GamePoint3D position, GamePoint2D size, TcpClientHandler client)
        {
            //this will ignore the max entity limit, however cannot ignore the max connection limit
            //returns if successful
            int i = 0;
            if (clientMap.Count < gameServer.MaxConnections)
            {
                while (entityMap.ContainsKey(i)) //looks to find the lowest open slot to make an entity
                    i += 1;
                entityMap.Add(i, new GameEntity(position, size, i, this));
                clientMap.Add(i, new GameClient(client, i, this));
            }
            return i;
        }
        public bool removeClient(int id)
        {
            if(clientMap.ContainsKey(id))
            {
                removeEntity(clientMap[id].entityId);
                clientMap.Remove(id);
                return true;
            }
            return false;
        }
        public void update()
        {
            if (clientMap.Count <= 0) return;
            bool canPass = false;
            List<int> hasDone = new List<int>();
            while (!canPass)
            {
                try
                {
                    foreach (KeyValuePair<int, GameClient> client in clientMap)
                    {
                        if (!hasDone.Contains(client.Key))
                        {
                            client.Value.update(this);

                            if ((mainProgram.gameSteps + (uint)client.Key) % 6 == 0)
                            {
                                client.Value.updatePriorities(entityMap);
                            }
                            hasDone.Add(client.Key);
                        }
                    }
                    canPass = true;
                }
                catch(InvalidOperationException)
                {
                    Console.WriteLine("error-client map probably changed");
                }
            }
            canPass = false;
            hasDone = new List<int>();
            while(!canPass)
            {
                try
                {
                    foreach(KeyValuePair<int,GameEntity> ent in entityMap)
                    {
                        if(!hasDone.Contains(ent.Key))
                        {
                            ent.Value.update(this);
                            hasDone.Add(ent.Key);
                        }
                    }
                    canPass = true;
                }
                catch(InvalidOperationException)
                {
                    Console.WriteLine("error-entity map probably changed");
                }
            }
            sendUpdates();
        }
        public void sendUpdates(List<int> alreadyDone = null)
        {
            if(alreadyDone == null)
            {
                alreadyDone = new List<int>();
            }
            try
            {
                foreach (KeyValuePair<int, GameClient> pair in clientMap)
                {
                    if (!alreadyDone.Contains(pair.Key))
                    {
                        int count = pair.Value.updatedQueue.Count;
                        BufferStream buff_ = new BufferStream(12 + (52 * count), 1);
                        buff_.Seek(0);
                        buff_.Write((ushort)0);
                        buff_.Write(pair.Value.updatedQueue.Count);
                        int updates = GameGeometry.clamp(96 - count, 0, 96);
                        while (updates < 96)
                        {
                            int ent_ = pair.Value.updatedQueue.Dequeue();
                            if (entityMap.ContainsKey(ent_))
                            {
                                GameEntity ent = entityMap[ent_];
                                buff_.Write(ent.id);
                                buff_.Write(ent.pos.X);
                                buff_.Write(ent.pos.Y);
                                buff_.Write(ent.pos.Z);
                                buff_.Write(ent.size.X);
                                buff_.Write(ent.size.Y);
                                buff_.Write(ent.direction);
                                buff_.Write(ent.pitch);
                                updates++;
                            }
                        }
                        sendToClient(pair.Value, buff_);
                        buff_.Deallocate();
                        alreadyDone.Add(pair.Key);
                    }
                }
            }
            catch(InvalidOperationException)
            {
                Console.WriteLine("error-client map probably changed");
                sendUpdates(alreadyDone);
            }
        }
        public int getPlayer(int socket)
        {
            foreach(KeyValuePair<int,GameClient> client in clientMap)
                if(socket == client.Value.clientHandler.Socket)
                    return client.Value.entityId;
            return -1;
        }
        public GameClient getClient(int id)
        {
            foreach(KeyValuePair<int,GameClient> client in clientMap)
                if(id == entityMap[client.Key].id)
                    return client.Value;
            return null;
        }
        public GameClient getClientFromSocket(string socket)
        {
            int socket_;
            try
            {
                socket_ = Convert.ToInt32(socket);
            }
            catch (FormatException)
            {
                Console.WriteLine("error-improper socket");
                return null;
            }
            return getClientFromSocket(socket_);
        }
        public GameClient getClientFromSocket(int socket)
        {
            foreach(KeyValuePair<int,GameClient> client in clientMap)
            {
                if(socket == client.Value.clientHandler.Socket)
                {
                    return client.Value;
                }
            }
            Console.WriteLine("error-invalid socket");
            return null;
        }
        public GameEntity getEntity(int id)
        {
            if (entityMap.ContainsKey(id))
                return entityMap[id];
            return null;
        }
        public void sendToAllClients(BufferStream buff, List<int> sentList = null)
        {
            if (buff != null)
            {
                if (sentList == null)
                    sentList = new List<int>();
                try
                {
                    foreach (KeyValuePair<int, GameClient> pair in clientMap)
                    {
                        if (!sentList.Contains(pair.Key))
                        {
                            sendToClient(pair.Value.clientHandler, buff);
                            sentList.Add(pair.Key);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("error-client map probably changed; attempting to send remaining data");
                    sendToAllClients(buff, sentList);
                }
            }
            else
            {
                Console.WriteLine("error-inputted buffer is null");
            }
        }
        public bool sendToClient(GameClient client, BufferStream buff)
        {
            return sendToClient(client.clientHandler, buff);
        }
        public bool sendToClient(TcpClientHandler client, BufferStream buff)
        {
            if (client != null && buff != null)
            {
                try
                {
                    if (client.Connected && client.Receiver.Connected && client.Stream.CanWrite)
                    {
                        PacketStream.SendAsync(client, buff);
                        return true;
                    }
                }
                catch (System.IO.IOException)
                {
                    Console.WriteLine("error-writing to stream for socket " + client.Socket.ToString() + Environment.NewLine + " probably failed, trying again");
                    return sendToClient(client, buff);
                }
            }
            else
            {
                if(client == null)
                {
                    Console.WriteLine("error-inputted client is null");
                }
                if(buff == null)
                {
                    Console.WriteLine("error-inputted buffer is null");
                }
            }
            return false;
        }
        public bool checkCollision(GamePoint3D d1, GamePoint3D d2)
        {
            try
            {
                foreach(KeyValuePair<int,GameObject> pair in objectMap)
                {
                    GamePoint3D p1 = pair.Value.position,
                                p2 = pair.Value.position + pair.Value.size;
                    if (GameGeometry.cube_in_cube(d1, d2, p1, p2))
                        return true;
                }
            }
            catch(InvalidOperationException)
            {
                //needs to try again since the objectMap was altered
                return checkCollision(d1, d2);
            }
            return false;
        }
    }
    public class GameEntity
    {
        public GamePoint3D pos;
        private GamePoint3D previous_pos;
        public GamePoint3D spd;
        public GamePoint3D base_spd;
        public GamePoint3D frc;
        public GamePoint2D size;
        public float direction, pitch, precision, previous_direction = 0f, previous_pitch = 0f;
        public int id { get; private set; }
        public GameEntity(GamePoint3D Position, GamePoint2D Size, int Id, GameWorld gameWorld)
        {
            pos = Position;
            id = Id;
            spd = new GamePoint3D();
            frc = new GamePoint3D(1.2d,1.2d,1.01d);      //friction system needs to be changed to support different areas, like just going through air 
            base_spd = new GamePoint3D(1d, 0.5d, 0.75d); //    or walking on something, which have different frictions
            direction = 0f;
            pitch = 0f;
            size = Size;
            precision = 1; //higher number = chunkier collision checks (faster but crappier)

            BufferStream buff = new BufferStream(1024, 1);
            buff.Write((ushort)4);
            buff.Write(id);
            buff.Write(pos.X);
            buff.Write(pos.Y);
            buff.Write(pos.Z);
            buff.Write(size.X);
            buff.Write(size.Y);
            buff.Write(direction);
            buff.Write(pitch);
            gameWorld.sendToAllClients(buff);
            buff.Deallocate();
        }
        public bool update(GameWorld gameWorld)
        {
            spd.Z -= 0.5;

            GamePoint3D sz3_ = new GamePoint3D(size.X, size.X, -size.Y);
            GamePoint3D d1 = pos + new GamePoint2D(-size.X, -size.X),
                        d2 = pos + new GamePoint3D(size.X, size.X, size.Y);

            if (spd.X != 0d)
            {
                GamePoint3D t_ = new GamePoint3D(spd.X, 0d, 0d);
                while (gameWorld.checkCollision(d1 + t_, d2 + t_))
                {
                    if (Math.Abs(spd.X) > precision)
                    {
                        spd.X = 0;
                        break;
                    }
                    spd.X -= precision * Math.Sign(spd.X);
                    t_.X = spd.X;
                }
            }
            if (spd.Y != 0d)
            {
                GamePoint3D t_ = new GamePoint3D(0d, spd.Y, 0d);
                while (gameWorld.checkCollision(d1 + t_, d2 + t_))
                {
                    if (Math.Abs(spd.Y) > precision)
                    {
                        spd.Y = 0;
                        break;
                    }
                    spd.Y -= precision * Math.Sign(spd.Y);
                    t_.Y = spd.Y;
                }
            }
            if (spd.Z != 0d)
            {
                GamePoint3D t_ = new GamePoint3D(0d, 0d, spd.Z);
                while (gameWorld.checkCollision(d1 + t_, d2 + t_))
                {
                    if (Math.Abs(spd.Z) > precision)
                    {
                        spd.Z = 0;
                        break;
                    }
                    spd.Z -= precision * Math.Sign(spd.Z);
                    t_.Z = spd.Z;
                }
            }

            pos += spd;
            spd = spd.Divide(frc);
            direction = direction % 360f;
            pitch = GameGeometry.clamp(pitch, -89f, 89f);
            bool updateSelf = false;
            if (pos != previous_pos || previous_direction != direction || previous_pitch != pitch)
            {
                updateSelf = true;
            }

            previous_pos = pos;

            return updateSelf;
        }
        public void getBoxWithSpeed(GamePoint3D d1, GamePoint3D d2, GamePoint3D spd, out GamePoint3D o1, out GamePoint3D o2)
        {
            o1 = d1.Min(d2);
            o2 = d2.Max(d1);
            if(spd.X > 0)
            {
                o2.X += spd.X;
            }
            else
            {
                o1.X += spd.X;
            }
            if(spd.Y > 0)
            {
                o2.Y += spd.Y;
            }
            else
            {
                o1.Y += spd.Y;
            }
            if(spd.Z > 0)
            {
                o2.Z += spd.Z;
            }
            else
            {
                o1.Z += spd.Z;
            }
        }
    }
    public class GameClient
    {
        public int entityId;
        public TcpClientHandler clientHandler;
        public InputMap inputMap;
        public Stopwatch pingWatch;
        private List<int> priorityList;
        private GamePoint3D tmppos;
        private Dictionary<int, GameEntity> tmpmap;
        public Queue<int> updatedQueue { get; private set; }
        public GameClient(TcpClientHandler tcpclient, int entityid, GameWorld gameWorld)
        {
            entityId = entityid;
            clientHandler = tcpclient;
            inputMap = new InputMap();
            pingWatch = new Stopwatch();
            priorityList = new List<int>();
            updatedQueue = new Queue<int>();
            updatePriorities(gameWorld.entityMap);
        }
        public void update(GameWorld gameWorld)
        {
            if (gameWorld.entityMap.ContainsKey(entityId))
            {
                if (clientHandler.Connected)
                {
                    GameEntity ent_ = gameWorld.entityMap[entityId];
                    float dir = ent_.direction;
                    if (inputMap.getInput("left") == 1f) //strafe left
                    {
                        ent_.spd.Add(GameGeometry.lengthdir(Convert.ToSingle(ent_.base_spd.Y), dir - 90));
                    }
                    if (inputMap.getInput("right") == 1f) //strafe right
                    {
                        ent_.spd.Add(GameGeometry.lengthdir(Convert.ToSingle(ent_.base_spd.Y), dir + 90));
                    }
                    if (inputMap.getInput("forward") == 1f) //forward
                    {
                        ent_.spd.Add(GameGeometry.lengthdir(Convert.ToSingle(ent_.base_spd.X), dir));
                    }
                    if (inputMap.getInput("backward") == 1f) //backward
                    {
                        ent_.spd.Add(GameGeometry.lengthdir(Convert.ToSingle(ent_.base_spd.Z), dir + 180));
                    }
                    if (inputMap.getInput("up") == 1f) //jump
                    {
                        GamePoint3D d1 = ent_.pos + new GamePoint3D(-ent_.size.X, -ent_.size.X, -1),
                                    d2 = ent_.pos + new GamePoint3D(ent_.size.X, ent_.size.X, ent_.size.Y - 1);
                        if (gameWorld.checkCollision(d1,d2))
                            ent_.spd.Z += 8d;
                    }
                    if (inputMap.getInput("down") == 1f) //crouch
                    {
                        ent_.spd.Z -= 1d; //currently just makes the player fall faster?
                    }
                    ent_.direction = inputMap.getInput("view_x");
                    ent_.pitch = inputMap.getInput("view_y");
                }
                else
                {
                    gameWorld.removeEntity(entityId);
                    gameWorld.removeClient(gameWorld.getPlayer(clientHandler.Socket));
                }
                ulong tmpStep = mainProgram.gameSteps;
                uint pos_ = 1;
                foreach(int thing in priorityList)
                {
                    if(tmpStep % pos_ == 0 && !updatedQueue.Contains(thing))
                    {
                        updatedQueue.Enqueue(thing);
                        //Console.WriteLine("added something to the queue");
                    }
                    pos_++;
                }
                
            }
        }
        public void updatePriorities(Dictionary<int,GameEntity> entityMap)
        {
            tmpmap = entityMap;
            List<int> newList = new List<int>();
            tmppos = entityMap[entityId].pos;
            foreach (KeyValuePair<int, GameEntity> pair in entityMap)
            {
                newList.Add(pair.Key);
            }
            newList.Sort(comparePriority);
            priorityList = newList;
        }
        private int comparePriority(int x, int y)
        {
            if(tmpmap[x] == null)
            {
                if(tmpmap[y] == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if(tmpmap[y] == null)
                {
                    return 1;
                }
                else
                {
                    double dist_ = GameGeometry.point_distance(tmppos, tmpmap[x].pos),
                        dist2_ = GameGeometry.point_distance(tmppos, tmpmap[y].pos);
                    return dist_.CompareTo(dist2_);
                }
            }
            
        }
    }
    public class InputMap
    {
        public Dictionary<string, float> map { get; private set; }
        public InputMap()
        {
            map = new Dictionary<string, float>();
        }
        public bool setInput(string key, bool state)
        {
            return setInput(key, state ? 1f : 0f);
        }
        public bool setInput(string key, float state)
        {
            return setInput(key, state, 1); //the third argument probably shouldn't be changed
        }
        private bool setInput(string key, float state, uint times)
        {
            //returns if successful

            bool allow_add = true;
            if (map.ContainsKey(key))
            {
                if (map[key] != state)
                {
                    map.Remove(key);
                }
                else
                {
                    allow_add = false;
                }
            }
            try
            {
                if(allow_add)
                    map.Add(key, state);
            }
            catch(ArgumentException e)
            {
                if (times >= 17)
                {
                    Console.WriteLine("pretty much failed to set input after 16 attempts");
                    return false;
                }
                else if (times > 1)
                    Console.WriteLine("error setting input, trying again (attempt #" + times.ToString() + ")");
                else if (times == 1)
                    Console.WriteLine("  error-key: " + key + ", val: " + state.ToString() + Environment.NewLine + e.ToString());
                return setInput(key, state, times + 1);
            }
            return true;
        }
        public float getInput(string key)
        {
            float val_ = 0f;
            try
            {
                if (map.ContainsKey(key))
                    val_ = map[key];
            }
            catch(KeyNotFoundException)
            {
                Console.WriteLine("error getting input");
            }
            return val_;
        }
    }
    public class GameObject
    {
        public GamePoint3D position;
        public GamePoint3D size;
        public int id;
        public GameObject(GamePoint3D Position, GamePoint3D Size, int Id, GameWorld gameWorld)
        {
            position = Position;
            size = Size;
            id = Id;

            BufferStream buff = new BufferStream(64,1);
            buff.Write((ushort)1);
            buff.Write((uint)id);
            buff.Write(position.X);
            buff.Write(position.Y);
            buff.Write(position.Z);
            buff.Write(size.X);
            buff.Write(size.Y);
            buff.Write(size.Z);
            gameWorld.sendToAllClients(buff);
            buff.Deallocate();
        }
    }
}
