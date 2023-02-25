using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace FileTransferServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 4343;
            IPAddress ipAddr = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, port);
            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                Console.WriteLine("Waiting for incoming connections...");

                while (true)
                {
                    Socket clientSocket = listener.Accept();
                    Console.WriteLine("Accepted new connection from {0}", clientSocket.RemoteEndPoint);

                    // Receive the filename
                    byte[] filenameBuffer = new byte[1024];
                    int bytesReceived = clientSocket.Receive(filenameBuffer);
                    string filename = System.Text.Encoding.ASCII.GetString(filenameBuffer, 0, bytesReceived);
                    Console.WriteLine("Received filename: {0}", filename);

                    // Send the expected file size
                    FileInfo fileInfo = new FileInfo(filename);
                    byte[] sizeBuffer = System.Text.Encoding.ASCII.GetBytes(fileInfo.Length.ToString());
                    clientSocket.Send(sizeBuffer);
                    Console.WriteLine("file size sent is:"+sizeBuffer);

                    // Open the file
                    FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);

                    // Send the file
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        clientSocket.Send(buffer, bytesRead, SocketFlags.None);
                    }

                    Console.WriteLine("File sent successfully");

                    // Clean up
                    fileStream.Close();
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}