using System;
using System.Text;
using System.IO;
using System.Net;

namespace GMS_Server
{
    public class SettingsSystem
    {
        public string settings_path { get; private set; }
        public int port { get; private set; }
        public int maxConnections {get; private set; }
        public int alignment { get; private set; }
        public uint timeout { get; private set; }
        public IPAddress address { get; private set; }
        public SettingsSystem()
        {
            port = 5524;
            maxConnections = 8;
            alignment = 1;
            timeout = 4096;
            address = null;
            settings_path = @".\server.txt";
        }
        public SettingsSystem(int Port, int MaxConnections, int Alignment, uint Timeout)
        {
            port = Port;
            maxConnections = MaxConnections;
            alignment = Alignment;
            timeout = Timeout;
            address = null;
            settings_path = @".\server.txt";
        }
        public SettingsSystem(int Port, int MaxConnections, int Alignment, uint Timeout, string Path, IPAddress Address = null)
        {
            port = Port;
            maxConnections = MaxConnections;
            alignment = Alignment;
            timeout = Timeout;
            address = Address;
            settings_path = Path;
        }

        public void ReadFile()
        {
            if (File.Exists(settings_path))
            {
                Console.WriteLine("Found a settings file");
                foreach (string line in File.ReadLines(settings_path))
                {
                    string nextCmd;
                    string firstCmd = CommandSystem.ReadCommand(line, out nextCmd);
                    int tmp_ = 0;

                    switch (firstCmd)
                    {
                        case "":
                            break;
                        case "port":
                            try
                            {
                                tmp_ = Convert.ToInt32(CommandSystem.ReadCommand(nextCmd));
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("error-invalid port in settings file");
                                break;
                            }
                            port = tmp_;
                            break;
                        case "ip":
                            IPAddress tmpIp;
                            string cmd_ = CommandSystem.ReadCommand(nextCmd);
                            if (cmd_ != "null")
                            {
                                try
                                {
                                    tmpIp = IPAddress.Parse(cmd_);
                                }
                                catch (FormatException)
                                {
                                    Console.WriteLine("error-invalid ip in settings file");
                                    break;
                                }
                            }
                            else
                            {
                                tmpIp = null;
                            }
                            address = tmpIp;
                            break;
                        case "maxplayers":
                            try
                            {
                                tmp_ = Convert.ToInt32(CommandSystem.ReadCommand(nextCmd));
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("error-invalid max players in settings file");
                                break;
                            }
                            maxConnections = tmp_;
                            break;
                        case "maxtimeout":
                            uint tmpu_;
                            try
                            {
                                tmpu_ = Convert.ToUInt32(CommandSystem.ReadCommand(nextCmd));
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine("error-invalid max timeout in settings file");
                                break;
                            }
                            timeout = tmpu_;
                            break;
                    }
                }
            }
            else
            {
                createSettingsFile();
            }
        }
        private void createSettingsFile(bool tried = false)
        {
            if(!tried)
                Console.WriteLine("No settings file found, attempting to create one, and using default settings");
            try
            {
                //File.Create(settings_path);
                //File.WriteAllText(settings_path, String.Format("ip null{0}port {1}{0}maxplayers {2}{0}maxtimeout {3}", Environment.NewLine, port, maxConnections, timeout));
                StreamWriter sw = new StreamWriter(settings_path);
                sw.WriteLine("ip null");
                sw.WriteLine("port {0}", port);
                sw.WriteLine("maxplayers {0}", maxConnections);
                sw.WriteLine("maxtimeout {0}", timeout);
                sw.Close();
            }
            catch (IOException)
            {
                Console.WriteLine("error-settings file in use, trying again");
                createSettingsFile(true);
            }
        }
    }
}
