/*
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
        SerialPacket packet;

        public class SimpleSerialPacket
        {
            public SimpleSerialPacket(byte[] data, byte address)
            {
                this.data = data;
                this.address = address;
            }

            private byte[] data;
            private byte address;

            public byte[] Data
            {
                get
                {
                    return data;
                }
            }

            public byte Address
            {
                get
                {
                    return address;
                }
            }

            public byte Length
            {
                get
                {
                    return (byte)data.Length;
                }
            }
        }

        private void RxPacketComplete(SerialPacket s)
        {
            //Console.WriteLine("Receive Packet Complete:");
            for (int i = 0; i < s.receive_length; i++)
            {
            }

            SimpleSerialPacket simple = new SimpleSerialPacket(s.receive_data.Take(s.receive_data_count).ToArray(), s.receive_address);
            if (newDataAvailable != null)
            {
                newDataAvailable(simple);
            }
        }

        public delegate void newDataAvailableDelegate(SimpleSerialPacket s);
        public newDataAvailableDelegate newDataAvailable;

        public delegate void receiveDataErrorDelegate(byte err);
        public receiveDataErrorDelegate receiveDataError;

        private void TxPacketComplete()
        {
        }

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
            packet.SerialByteTransmitComplete();
        }

        private void RxPacketError(byte err)
        {
            //Console.WriteLine("Error! Byte Stream: ");
            //foreach (byte b in packet.receive_data)
            //{
            //    Console.Write(b + ", ");
            //}

            if (receiveDataError != null)
            {
                receiveDataError(err);
            }
        }

        public SerialPortWrapper()
        {
            // Setup method delegates
            packet = new SerialPacket();
            packet.ReceiveDataError = new SerialPacket.ReceiveDataErrorDelegate (RxPacketError);
            packet.Transmit = new SerialPacket.TransmitDelegate(TxByte);
            packet.TransmitPacketComplete = new SerialPacket.TransmitPacketeCompleteDelegate(TxPacketComplete);
            packet.ReceivePacketComplete = new SerialPacket.ReceivePacketCOmpleteDelegate(RxPacketComplete);

            // Setup the serial port defaults
            port = new SerialPort();
            port.BaudRate = 9600;
            port.DataBits = 8;
            port.Parity = Parity.None;
            port.Handshake = Handshake.None;
            port.StopBits = StopBits.One;
            //port.DiscardNull = false; // not implemented in Mono, just don't use
            port.DtrEnable = false;
            port.RtsEnable = false;
            port.Encoding = System.Text.Encoding.Default;

            // Earlier versions of Mono don't fire this event, so poll instead.
            port.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
            System.Timers.Timer t = new System.Timers.Timer(10);
            //t.Elapsed += t_Elapsed;
            //t.Start();
        }

        void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            port_DataReceived(null, null);
        }

        public void Transmit(byte[] data, byte address)
        {
            // Wait until all pending data is sent
            while (packet.SerialTransferInProgress())
            {
            }
            data.CopyTo(packet.transmit_data, 0);
            packet.SerialTransmit(address, (byte)data.Length);
        }

        void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                while (port.IsOpen && port.BytesToRead > 0)
                {
                    byte data = (byte)port.ReadByte();
                    packet.ProcessDataChar(data);
                }
            }
            catch (Exception ex)
            {
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
