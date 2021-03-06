﻿
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Heijden.DNS;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.IO;


namespace nfaTray
{

    public class nfaServers
    {

        public static List<nfaServer> GetServers()
        {

            var list = new List<string>();

            Resolver resolver = new Resolver();
            Response response = resolver.Query("servers.nfaw.netfree.link", QType.TXT);
            foreach (AnswerRR answerRR in response.Answers)
            {
                var str = answerRR.RECORD.ToString();
                list.Add(str.Substring(1, str.Length - 2));
            }


            var listServers = new List<nfaServer>();

            foreach (string item in list)
            {
                var tmp = item.Split('|');

                if (tmp.Length > 1)
                {
                    var server = new nfaServer
                    {
                        Country = tmp[1],
                        Host = tmp[0],
                        Latency = -1
                    };
                    listServers.Add(server);
                }
            }

            return listServers;

        }

        public static void findFastHost(string[] nameOrAddress, Action<string, long> callback)
        {
            object lockAction = new Object();
            bool run = false;
            foreach (var item in nameOrAddress)
            {
                PingHostTime(item, (t) =>
                {
                    lock (lockAction)
                    {
                        if (run) return;
                        run = true;
                        callback.Invoke(item, t);
                    }
                });
            }

        }

        public static void findOpenPortUdp(string host, int[] ports, Action<int> callback)
        {
            object lockAction = new Object();
            bool run = false;
            int endCount = 0;
            foreach (var port in ports)
            {
                new System.Threading.Thread(() =>
                {
                    try
                    {
                        using (var udpClient = new UdpClient())
                        {

                            udpClient.Connect(host, port);


                            byte[] receiveBytes = null;
                            IPEndPoint nfReceive = null;
                            nfReceive = new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 0);
                            udpClient.Client.ReceiveTimeout = 2000;

                            byte[] sendBytes = new byte[14];
                            (new RNGCryptoServiceProvider()).GetBytes(sendBytes);
                            sendBytes[0] = 0x38;
                            sendBytes[9] = sendBytes[10] = sendBytes[11] = sendBytes[12] = sendBytes[13] = 0;
                            udpClient.Send(sendBytes, sendBytes.Length);
                            try
                            {
                                receiveBytes = udpClient.Receive(ref nfReceive);
                            }
                            catch (Exception) { }

                            if (receiveBytes != null)
                            {
                                lock (lockAction)
                                {
                                    if (!run)
                                    {
                                        run = true;
                                        callback.Invoke(port);
                                    }
                                }
                            }

                        }
                    }
                    catch (Exception)
                    {


                    }
                    
                    endCount++;
                    if (endCount == ports.Length && !run)
                    {
                        run = true;
                        callback.Invoke(-1);
                    }

                }).Start();
            }


        }

        public static void findOpenPortTcp(string host, int[] ports, Action<int> callback)
        {
            object lockAction = new Object();
            bool run = false;
            int endCount = 0;
            foreach (var port in ports)
            {
                new System.Threading.Thread(() =>
                {
                    try
                    {
                        using (var tcpClient = new TcpClient())
                        {
                            tcpClient.ReceiveTimeout = tcpClient.SendTimeout = 10000;
                            tcpClient.Connect(host, port);
 
 
                            Stream mainStream = tcpClient.GetStream();

                            byte[] sendBytes = new byte[16];
                            (new RNGCryptoServiceProvider()).GetBytes(sendBytes);
                            //00 0e 38 00 5e 8b 46 41  65 71 87 00 00 00 00 00
                            sendBytes[0] = 0;
                            sendBytes[0+1] = 0x0e;
                            sendBytes[0+2] = 0x38;
                            sendBytes[9+2] = sendBytes[10+2] = sendBytes[11+2] = sendBytes[12+2] = sendBytes[13+2] = 0;

                            mainStream.Write(sendBytes, 0, sendBytes.Length);

                            StreamReader reader = new StreamReader(mainStream);

                            char[] readByte = new char[30];

                            int readCount = reader.Read(readByte,0,readByte.Length);


                            if (readCount > 3 && readByte[2] == 0x40)
                            {
                                lock (lockAction)
                                {
                                    if (!run)
                                    {
                                        run = true;
                                        callback.Invoke(port);
                                    }
                                }
                            }

                            reader.Close();
                            mainStream.Close();
                            tcpClient.Close();
                        }
                    }
                    catch (Exception)
                    {


                    }

                    endCount++;
                    if (endCount == ports.Length && !run)
                    {
                        run = true;
                        callback.Invoke(-1);
                    }

                }).Start();
            }


        }

        public static void PingHostTime(string nameOrAddress, Action<long> callback)
        {
            new System.Threading.Thread(() =>
            {
                Ping pinger = new Ping();
                try
                {
                    PingReply reply = pinger.Send(nameOrAddress, 1000);

                    if (reply.Status == IPStatus.Success)
                    {
                        callback.Invoke(reply.RoundtripTime);
                        return;
                    }
                }
                catch (PingException generatedExceptionName)
                {

                }
                callback.Invoke(-1);
            }).Start();
        }
    }
    public class nfaServer
    {
        public long Latency { get; set; }
        public string Country { get; set; }
        public string Host { get; set; }
    }


}
