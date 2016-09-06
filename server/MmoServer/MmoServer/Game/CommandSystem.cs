using System;
using System.Collections.Generic;
using SharpServer.Buffers;
using SharpServer.Sockets;

namespace GMS_Server
{
    public static class CommandSystem
    {
        private static string text = "";

        public static string Update()
        {
            string final = null;
            if(Console.KeyAvailable)
            {
                ClearCurrentConsoleLine();
                
                ConsoleKeyInfo cki = Console.ReadKey();
                if (cki.Key == ConsoleKey.Backspace)
                {
                    if(text.Length > 0)
                    {
                        text = text.Remove(text.Length - 1);
                    }
                }
                else if(cki.Key == ConsoleKey.Delete)
                {
                    if(text.Length > 0 && Console.CursorLeft + 1 <= text.Length)
                    {
                        text = text.Remove(Console.CursorLeft, 1);
                    }
                }
                else if(cki.Key == ConsoleKey.Enter)
                {
                    final = text;
                    text = "";
                }
                else if(cki.Key != ConsoleKey.Escape)
                {
                    text += cki.KeyChar;
                }
                if (final == null)
                    Console.Write(">" + text);
                else
                    Console.Write(text);
            }
            return final;
        }
        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }
        public static string ReadCommand(string fullCommand)
        {
            string nothing;
            return ReadCommand(fullCommand, out nothing);
        }
        public static string ReadCommand(string fullCommand, out string newFullCommand)
        {
            string final = "",
                finalFull = "";
            if (fullCommand != null)
            {
                bool finished = false;
                char[] chars = fullCommand.ToCharArray();
                for (int i = 0; i < fullCommand.Length; i += 1)
                {
                    if (!finished)
                    {
                        if (chars[i] == ' ')
                        {
                            finished = true;
                        }
                        else
                        {
                            final += chars[i];
                        }
                    }
                    else
                    {
                        finalFull += chars[i];
                    }
                }
            }
            newFullCommand = finalFull;
            return final;
        }
        public static void DoCommand(string consoleInput)
        {
            List<string> cmdList = new List<string>();
            while (consoleInput != "")
            {
                cmdList.Add(CommandSystem.ReadCommand(consoleInput, out consoleInput));
            }
            GameClient client_;
            if (cmdList.Count == 0) return;
            switch (cmdList[0])
            {
                case "":
                    break;
                case "ping":
                    if (cmdList.Count != 2)
                        break;
                    client_ = mainProgram.gameWorld.getClientFromSocket(cmdList[1]);
                    if (client_ != null)
                    {
                        BufferStream buff_ = new BufferStream(8, 1);
                        buff_.Write((ushort)6);
                        mainProgram.gameWorld.sendToClient(client_, buff_);
                        client_.pingWatch.Reset();
                        client_.pingWatch.Start();
                        buff_.Deallocate();
                    }
                    break;
                case "teleport":
                    if (cmdList.Count != 5)
                        break;
                    client_ = mainProgram.gameWorld.getClientFromSocket(cmdList[1]);
                    if (client_ == null)
                        break;
                    double x_, y_, z_;
                    GameEntity ent_ = mainProgram.gameWorld.getEntity(client_.entityId);
                    try
                    {
                        if (cmdList[2] == "~")
                            x_ = ent_.pos.X;
                        else
                            x_ = Convert.ToDouble(cmdList[2]);
                        if (cmdList[3] == "~")
                            y_ = ent_.pos.Y;
                        else
                            y_ = Convert.ToDouble(cmdList[3]);
                        if (cmdList[4] == "~")
                            z_ = ent_.pos.Z;
                        else
                            z_ = Convert.ToDouble(cmdList[4]);
                    }
                    catch (FormatException)
                    {
                        mainProgram.WriteLine("error-improper coordinate(s)");
                        break;
                    }
                    
                    ent_.pos = new GamePoint3D(x_, y_, z_);
                    break;
                case "players":
                    mainProgram.WriteLine("Current players [" + mainProgram.gameWorld.clientMap.Count.ToString() + @"\" + mainProgram.settings.maxConnections.ToString() + "]:");
                    for (int i = 0; i < mainProgram.gameWorld.clientMap.Count; i++)
                    {
                        mainProgram.WriteLine(" -#{0}. Socket {1}, ip {2}", i, mainProgram.gameWorld.clientMap[i].clientHandler.Socket, mainProgram.getIp(mainProgram.gameWorld.clientMap[i].clientHandler).ToString());
                    }
                    if (mainProgram.gameWorld.clientMap.Count == 0)
                    {
                        mainProgram.WriteLine(" -Currently no players in this server");
                    }
                    break;
                case "stop":
                    mainProgram.game_server.Close();
                    break;
                case "help":
                    mainProgram.WriteLine("commands:{0}{1}{0}{2}{0}{3}{0}{4}{0}{5}",Environment.NewLine,
                         " -ping [socket] //pings a socket, writes the ping",
                         " -teleport [socket] [x] [y] [z] //teleports a player to a location",
                         " -players //lists all of the clients currently in the server",
                         " -help //lists all the commands available",
                         " -stop //closes the server");
                    break;
                default:
                    string outCmd = cmdList[0] + " [";
                    for (int i = 1; i < cmdList.Count - 1; i++)
                    {
                        outCmd += cmdList[i] + ",";
                    }
                    outCmd += cmdList[cmdList.Count - 1];
                    mainProgram.WriteLine("unknown command " + outCmd + "]");
                    break;
            }
        }
    }
}
