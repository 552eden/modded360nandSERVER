using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Reflection.Metadata.Ecma335;

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

        // Function to save a file
        static string saveFile()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            // Set initial directory (optional)
            saveFileDialog.InitialDirectory = Environment.CurrentDirectory;

            // Set the title of the dialog
            saveFileDialog.Title = "Save File As";

            // Filter for specific file types (optional)
            saveFileDialog.Filter = "Nand Files (*.bin)|*.bin|All files (*.*)|*.*";

            // Show the dialog and check if the user clicked OK
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Get the selected file name
                return saveFileDialog.FileName;
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


        static int recieveFileFromXbox(Socket clientSocket)
        {
            string filename = "";
            Console.WriteLine("Overriding file name with users choice");
            int md5same = 0;

            try
            {
                Thread thread = new Thread(() => filename = saveFile());
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

            try
            {
                // Send the start message
                byte[] startMessage = Encoding.ASCII.GetBytes("START_TRANSMISSION");
                clientSocket.Send(startMessage);




                // Receive file size
                byte[] fileSizeBuffer = new byte[4];
                clientSocket.Receive(fileSizeBuffer, 4, SocketFlags.None);
                int fileSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(fileSizeBuffer, 0));
                Console.WriteLine("Received file size: " + fileSize);

                // Receive additional data (string)
                byte[] additionalDataLengthBuffer = new byte[4];
                clientSocket.Receive(additionalDataLengthBuffer, 4, SocketFlags.None);
                int additionalDataLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(additionalDataLengthBuffer, 0));
                byte[] additionalDataBuffer = new byte[additionalDataLength];
                clientSocket.Receive(additionalDataBuffer, additionalDataLength, SocketFlags.None);
                string additionalData = Encoding.ASCII.GetString(additionalDataBuffer);
                Console.WriteLine("Received additional data: " + additionalData);

                // Receive file data
                using (FileStream fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    int totalBytesRead = 0;
                    while ((bytesRead = clientSocket.Receive(buffer, 1024, SocketFlags.None)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        if (totalBytesRead >= fileSize)
                            break;
                    }
                    Console.WriteLine("Received file data");
                }

                // Calculate MD5 hash
                string md5Hash = CalculateMD5(filename);

                if (md5Hash == additionalData)
                {
                    Console.WriteLine("Hashes are the same! Hash: {0}", md5Hash);
                    md5same = 1;
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    return 1;
                }
                else
                {
                    Console.WriteLine("hashes are not the same!");
                    Console.WriteLine("calculated Hash: {0}", md5Hash);
                    Console.WriteLine("Recived Hash: {0} ", additionalData);
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    return 0;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException: " + ex.ErrorCode + ", " + ex.Message);
                Console.WriteLine("Socket closed from xbox 360 side, this should be fine");
                if (md5same == 1) { return 1; }
                else
                {
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return 0;
            }

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
            int recievedFileResult = 0;
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
                        recievedFileResult = recieveFileFromXbox(clientSocket);
                        if (recievedFileResult == 1) { Console.WriteLine("recieved file seccesfully"); }
                        else { Console.WriteLine("error sending file"); }

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