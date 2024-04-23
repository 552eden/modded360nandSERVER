using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;
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

        static void printIP()
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                Console.WriteLine($"Interface: {networkInterface.Name}");

                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation ip in ipProperties.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) // Check for IPv4 address
                    {
                        Console.WriteLine($"  IPv4 Address: {ip.Address}");
                    }
                }
                Console.WriteLine();
            }
        }

        static int sendFileToXbox(Socket clientSocket)
        {
            string filename = "";
            Console.WriteLine("Overriding file name with users choice");

            try
            {
                Thread thread = new Thread(() => filename = SelectFile());
                thread.SetApartmentState(ApartmentState.STA); //Set the thread to STA
                thread.Start();
                thread.Join(); //Wait for the thread to end
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (!string.IsNullOrEmpty(filename))
            {
                Console.WriteLine($"Selected file: {filename}");
                // Process the selected file here
            }
            else
            {
                Console.WriteLine("Operation canceled.");
                // Clean up
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                return 0;
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
            Console.WriteLine("file size sent is: {0}", fileInfo.Length.ToString());

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
            fileStream.Close();
            System.Threading.Thread.Sleep(500);
            // Send the MD5
            byte[] sentMD5 = System.Text.Encoding.ASCII.GetBytes(md5Hash);
            clientSocket.Send(sentMD5);
            Console.WriteLine("MD5 Sent is: {0}", md5Hash);


            // Clean up
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();

            return 1;
        }
    



        static void Main(string[] args)
        {
            int port = 4343;
            IPAddress ipAddr = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddr, port);
            Socket listener = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("your IP addresses are:");
            printIP();
            Console.WriteLine("please use the IP in the same network as your xbox");
            int sentFileResult = 0;
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                Console.WriteLine("Waiting for incoming connections...");

                while (true)
                {
                    Socket clientSocket = listener.Accept();
                    Console.WriteLine("Accepted new connection from {0}", clientSocket.RemoteEndPoint);
                    // Receive the filename to check if we need to send file or rceieve it

                    byte[] filenameBuffer = new byte[1024];
                    int bytesReceived = clientSocket.Receive(filenameBuffer);
                    string filename = System.Text.Encoding.ASCII.GetString(filenameBuffer, 0, bytesReceived);
                    Console.WriteLine("file name test");
                    Console.WriteLine(filename);
                    Console.WriteLine("end filename test");
                    Console.WriteLine("Received filename: {0}", filename);
                    if(filename == "game:\\updflash.bin")
                    {
                        sentFileResult = sendFileToXbox(clientSocket);
                        if(sentFileResult == 1) { Console.WriteLine("sent file seccesfully"); }
                        else { Console.WriteLine("error sending file");  }
                    }
                    else if (filename == "game:\\flashdmp.bin")
                    {
                        Console.WriteLine("inset file receiving here");

                    }
                    

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        
    }
}