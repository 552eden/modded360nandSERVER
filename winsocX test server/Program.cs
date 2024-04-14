﻿using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace FileTransferServer
{
    class Program
    {

        static string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    // Compute MD5 hash of the file stream
                    byte[] hashBytes = md5.ComputeHash(stream);

                    // Convert the byte array to hexadecimal string
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
        }

        // Function to select a file
        static string SelectFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set initial directory (optional)
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;

            // Set the title of the dialog
            openFileDialog.Title = "Select a File";

            // Filter for specific file types (optional)
            openFileDialog.Filter = "Nand Files (*.bin)|*.bin|All files (*.*)|*.*";

            // Show the dialog and check if the user clicked OK
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Get the selected file name
                return openFileDialog.FileName;
            }
            else
            {
                // User canceled the operation
                return null;
            }
        }



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
                    Console.WriteLine("Overriding file name with users choice");

                    
                    Thread thread = new Thread(() => filename = SelectFile());
                    thread.SetApartmentState(ApartmentState.STA); //Set the thread to STA
                    thread.Start();
                    thread.Join(); //Wait for the thread to end

                    if (!string.IsNullOrEmpty(filename))
                    {
                        Console.WriteLine($"Selected file: {filename}");
                        // Process the selected file here
                    }
                    else
                    {
                        Console.WriteLine("Operation canceled.");
                    }
                    Console.WriteLine($"Selected file: {Path.GetFileName(filename)}");

                    // Calculate MD5 hash
                    string md5Hash = CalculateMD5(filename);

                    // Print the result
                    Console.WriteLine("MD5 hash of the file:");
                    Console.WriteLine(md5Hash);


                    // Send the expected file size
                    FileInfo fileInfo = new FileInfo(filename);
                    byte[] sizeBuffer = System.Text.Encoding.ASCII.GetBytes(fileInfo.Length.ToString());
                    clientSocket.Send(sizeBuffer);
                    Console.WriteLine("file size sent is: {0}", sizeBuffer);

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