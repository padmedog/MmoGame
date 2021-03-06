﻿using System;
using System.Net;
using System.Net.Sockets;
using SharpServer.Sockets;
using GMS_Server;

namespace SharpServer.Buffers {
    /// <summary>
    /// Provides an interface for sending packets to a TCP client.
    /// </summary>
    public static class PacketStream {
        /// <summary>
        /// Asynchronously sends a buffer(packet) through the specified stream.
        /// </summary>
        /// <param name="stream">The particular stream of a TCP client to send through.</param>
        /// <param name="buffer">Buffer containing the packet to be sent.</param>
        /// <param name="size">The size of the buffer to be sent.</param>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.IO.IOException"/>
        /// <exception cref="System.ObjectDisposedException"/>
        public static async void SendAsync( NetworkStream stream, BufferStream buffer ) {
            stream.Write( buffer.Memory, 0 , buffer.Iterator );
            await stream.FlushAsync();
        }
        public static async void SendAsync(TcpClientHandler client, BufferStream buffer)
        {
            if (client.Connected && client.Receiver.Connected)
            {
                try
                {
                    client.Stream.Write(buffer.Memory, 0, buffer.Iterator);
                    await client.Stream.FlushAsync();
                }
                catch(System.IO.IOException e)
                {
                    mainProgram.WriteLine(e.ToString());
                }
            }
        }

        /// <summary>
        /// Synchronously sends a buffer(packet) through the specified stream.
        /// </summary>
        /// <param name="stream">The particular stream of a TCP client to send through.</param>
        /// <param name="buffer">Buffer containing the packet to be sent.</param>
        /// <exception cref="System.ArugmentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.IO.IOException"/>
        /// <exception cref="System.ObjectDisposedException"/>
        public static void SendSync( NetworkStream stream, BufferStream buffer ) {
            stream.Write( buffer.Memory, 0 , buffer.Iterator );
            stream.Flush();
        }
        public static void SendSync(NetworkStream stream, BufferStream buffer, int size)
        {
            stream.Write(buffer.Memory, 0, size);
            stream.Flush();
        }
    }
}
