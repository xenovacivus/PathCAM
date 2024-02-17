﻿/*
 * SerialPackets - A simple byte stuffed packetizer for async serial.
 * Copyright (C) 2010-2013  Benjamin R. Porter
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see [http://www.gnu.org/licenses/].
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Serial
{
    public class SerialPortWrapper
    {
        public SerialPort port;

        public delegate void newDataAvailableDelegate(byte[] data);
        public newDataAvailableDelegate newDataAvailable;

        private void TxByte(byte data)
        {
            if (port.IsOpen)
            {
                try
                {
                    port.Write(new byte[] { data }, 0, 1);
                    //Console.WriteLine("Tx'd byte = " + data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        System.Timers.Timer portPollTimer;
        public SerialPortWrapper()
        {
            // Setup the serial port defaults
            port = new SerialPort();
            port.BaudRate = 115200;
            port.DataBits = 8;
            port.Parity = Parity.None;
            port.Handshake = Handshake.None;
            port.StopBits = StopBits.One;
            //port.DiscardNull = false; // not implemented in Mono, just don't use
            port.DtrEnable = false;
            port.RtsEnable = false;
            port.Encoding = System.Text.Encoding.Default;

            
            port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
            // Earlier versions of Mono don't fire this event, so poll instead.
            // Added a lock, so polling and the proper callback should work together.
            portPollTimer = new System.Timers.Timer(10);
            portPollTimer.Elapsed += portPollTimer_Elapsed;
            portPollTimer.Start();
        }

        void portPollTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            portPollTimer.Stop();
            port_DataReceived(null, null);
            portPollTimer.Start();
        }

        public void Transmit(byte[] data)
        {
            //Console.WriteLine("SERIAL TX: '" + System.Text.Encoding.UTF8.GetString(data) + "'");
            port.Write(data, 0, data.Length);
        }

        object portLockObject = new object();
        void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (portLockObject)
            {
                try
                {
                    int bytesToRead;
                    while (port.IsOpen && (bytesToRead = port.BytesToRead) > 0)
                    {
                        byte[] buffer = new byte[bytesToRead];
                        port.Read(buffer, 0, bytesToRead);

                        // This works, but not as fast.
                        //byte[] buffer = new byte[1];
                        //buffer[0] = (byte)port.ReadByte();

                        if (newDataAvailable != null)
                        {
                            newDataAvailable(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception processing serial data: " + ex.Message);
                }
            }
        }

        public void Open(string portName, int baudRate)
        {
            Close();
            port.BaudRate = baudRate;
            port.PortName = portName;
            port.Open();

        }

        public void Close()
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }

        public bool IsOpen
        {
            get
            {
                return port.IsOpen;
            }
        }

        public string[] PortNames
        {
            get
            {
                // TODO: customize this list to show all possible devices on Mono
                return SerialPort.GetPortNames();
            }
        }

        public int BaudRate
        {
            get
            {
                return port.BaudRate;
            }
        }

        public string PortName
        {
            get
            {
                return port.PortName;
            }
        }
    }
}
