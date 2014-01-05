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
using System.Linq;
using System.Text;

namespace Serial
{
    class SerialPacket
    {
        /** Serial Packet Definition:
        //
        // START_BYTE     start byte
        // address
        // length
        // data(n bytes)
        // Checksum ~(address + length + data)

        // Escape Character and Byte Stuffing:
        // Any control character is replaced
        // with an escape character followed
        // by it's "escaped" value.  All data
        // in the transmission except for the
        // start byte can be escaped.  This
        // means the transmission may take up
        // to twice as long as expected from
        // just transmitting escape character
        **/


        public delegate void TransmitDelegate(byte data);
        public delegate void TransmitPacketeCompleteDelegate();
        public delegate void ReceivePacketCOmpleteDelegate(SerialPacket s);
        public delegate void ReceiveDataErrorDelegate(byte errCode);

        //public class SerialData
        //{
            public SerialPacket()
            {
                /* Receive State Variables */
                receive_state = PROC_STATE_AWAITING_START_BYTE;
                receive_next_char_is_escaped = false;

                /* Function Pointers */
                Transmit = null;
                TransmitPacketComplete = null;
                ReceivePacketComplete = null;
                ReceiveDataError = null;

                /* Transmit State Variables */
                transmit_state = PROC_STATE_TRANSMIT_COMPLETE;
                transmit_address = 0;
                transmit_length = 0;
                transmit_checksum = 0;
                transmit_data_index = 0;
            }

            /**
             * Transmit serial data
             * @param address The address to send the data to
             * @param length Number of bytes in data to be sent
             * @param maxLength Number of bytes allocated for
             *      the data array.
             * @return 0 for success, nonzero for failure.
             */
            public int SerialTransmit(byte address, byte length)
            {
                if (SerialTransferInProgress())
                {
                    return -1;
                }

                if (Transmit == null)
                {
                    return -2;
                }

                if (length > SERIAL_TRANSMIT_BUFFER_SIZE)
                {
                    return -3;
                }

                transmit_address = address;
                transmit_length = length;
                transmit_data_index = 0;// (byte*)transmit_data;
                transmit_escaped_char = 0;
                transmit_state = PROC_STATE_TRANSMIT_ADDRESS;

                Transmit(START_BYTE);

                return 0;
            }

        
            /** Call this method on USART data
             * transmit complete.
             */
            public void SerialByteTransmitComplete()
            {
                byte dataToTx = 0;

                // Check if we need to transmit an escaped character:
                if (transmit_escaped_char != 0)
                {
                    dataToTx = transmit_escaped_char;
                    transmit_escaped_char = 0;

                }
                else
                {
                    switch (transmit_state)
                    {
                        case PROC_STATE_TRANSMIT_ADDRESS:
                            dataToTx = transmit_address;
                            transmit_checksum = dataToTx;
                            transmit_state = PROC_STATE_TRANSMIT_LENGTH;
                        break;

                        case PROC_STATE_TRANSMIT_LENGTH:
                            dataToTx = transmit_length;
                            transmit_checksum += dataToTx;
                            transmit_state = PROC_STATE_TRANSMIT_DATA;
                        break;

                        case PROC_STATE_TRANSMIT_DATA:
                            dataToTx = transmit_data[transmit_data_index];// *(transmit_data_ptr);
                            transmit_checksum += dataToTx;
                            transmit_data_index++;
                            transmit_length--;
                            if (transmit_length == 0)
                            {
                                transmit_state = PROC_STATE_TRANSMIT_CHECKSUM;
                            }
                        break;

                        case PROC_STATE_TRANSMIT_CHECKSUM:
                            dataToTx = (byte)~transmit_checksum;
                            transmit_state = PROC_STATE_TRANSMIT_ALMOST_COMPLETE;
                        break;

                        case PROC_STATE_TRANSMIT_ALMOST_COMPLETE:
                            // Done transmitting!
                            transmit_state = PROC_STATE_TRANSMIT_COMPLETE;
                            if (TransmitPacketComplete!=null)
                            {
                                TransmitPacketComplete();
                            }
                            return;
                        break;

                        default:
                            // Shouldn't ever get here.
                        break;
                    }

                    // Check for control characters
                    switch(dataToTx)
                    {
                        case START_BYTE:
                            transmit_escaped_char = START_BYTE_ESCAPED;
                            dataToTx = ESCAPE_CHAR;
                        break;

                        case ESCAPE_CHAR:
                            transmit_escaped_char = ESCAPE_CHAR_ESCAPED;
                            dataToTx = ESCAPE_CHAR;
                        break;

                        case null_BYTE:
                            transmit_escaped_char = null_BYTE_ESCAPED;
                            dataToTx = ESCAPE_CHAR;
                        break;

                        case MAX_BYTE:
                            transmit_escaped_char = MAX_BYTE_ESCAPED;
                            dataToTx = ESCAPE_CHAR;
                        break;

                        default:
                            transmit_escaped_char = 0;
                        break;
                    }
                }

                // Transmit the data!
                if (Transmit!=null)
                {
                    Transmit(dataToTx);
                }

            }

        
            /** Processes a character from a serial stream
             * and reconstructs packet
             * @param data The next character in the stream
             */
            public void ProcessDataChar(byte data)
            {
                /* Unstuff bytes and locate start bytes here */

                /* See if the data received is value to ignore
                 * This most likely occurs in conjunction with
                 * a frame error: start byte detected, but no
                 * valid data afterward */
                if (data == null_BYTE || data == MAX_BYTE)
                {
                    SerialError(ERR_RECEIVED_IGNORE_BYTE);
                    return;
                }


                /* If any start byte is found, any current data
                 * transfer will be reset, and a new data transfer
                 * will begin.
                 */
                if (data == START_BYTE) /* Start byte */
                {
                    if (receive_state != PROC_STATE_AWAITING_START_BYTE)
                    {
                        SerialError(ERR_START_BYTE_INSIDE_PACKET);
                    }

                    /* Reset state */
                    receive_state = PROC_STATE_AWAITING_ADDRESS;
                    receive_data_count = 0;
                    receive_next_char_is_escaped = false;
                }
                else
                {
                    if (receive_state == PROC_STATE_AWAITING_START_BYTE)
                    {
                        SerialError(ERR_UNEXPECTED_START_BYTE);
                        //printf("Unexpected Start Byte: Expected 0x%x, Got 0x%x\n", START_BYTE, data);
                    }
                    else
                    {
                        /* Otherwise, unstuff bytes and send data to the state machine */
                        if (data == ESCAPE_CHAR) // Escape Character
                        {
                            receive_next_char_is_escaped = true;
                        }
                        else
                        {
                            if (receive_next_char_is_escaped)
                            {
                                receive_next_char_is_escaped = false;
                                switch (data)
                                {
                                    case ESCAPE_CHAR_ESCAPED:
                                        data = ESCAPE_CHAR;
                                        break;

                                    case START_BYTE_ESCAPED:
                                        data = START_BYTE;
                                        break;

                                    case null_BYTE_ESCAPED:
                                        data = null_BYTE;
                                        break;

                                    case MAX_BYTE_ESCAPED:
                                        data = MAX_BYTE;
                                        break;
                                }
                            }
                            SerialStateMachineProcess(data);
                        }
                    }
                }
            }

            private void SerialStateMachineProcess(byte data)
            {
                switch (receive_state)
                {
                    case PROC_STATE_AWAITING_ADDRESS:
                        receive_address = data;
                        receive_checksum = data;
                        receive_state = PROC_STATE_AWAITING_LENGTH;
                    break;

                    case PROC_STATE_AWAITING_LENGTH:
                        if (data > SERIAL_RECEIVE_BUFFER_SIZE)
                        {
                            /* Error, length too long.  Ignore packet. */
                            receive_state = PROC_STATE_AWAITING_START_BYTE;

                            /* Look for the next start byte.  Note: this
                             * will likey produce unexpected start byte error
                             */
                            SerialError(ERR_EXCESSIVE_PACKET_LENGTH);
                        }
                        else
                        {
                            receive_length = data;
                            receive_checksum += data;
                            receive_state = PROC_STATE_AWAITING_DATA;
                        }
                    break;

                    case PROC_STATE_AWAITING_DATA:

                        receive_length--;

                        receive_checksum += data;
                        receive_data[receive_data_count] = data;
                        receive_data_count++;

                        if (receive_length == 0)
                        {
                            receive_state = PROC_STATE_AWAITING_CHECKSUM;
                        }

                    break;

                    case PROC_STATE_AWAITING_CHECKSUM:
                        receive_checksum = (byte)~receive_checksum;
                        if (data == receive_checksum)
                        {
                            if (ReceivePacketComplete != null)
                            {
                                ReceivePacketComplete (this);
                            }
                        }
                        else
                        {
                            SerialError(ERR_CHECKSUM_MISMATCH);
                            //printf("Error: Checksum Mismatch.  Expected 0x%x, Got 0x%x\n", receive_checksum, data);
                        }
                        receive_state = PROC_STATE_AWAITING_START_BYTE;
                    break;

                    default:
                        // (It'll never get here)
                    break;
                }
            }

        
            /** Serial Packet Transfer Query Function
             * @return true if a packet transfer is currently
             *      in progress, false otherwise.
             */
            public bool SerialTransferInProgress()
            {
                return (transmit_state != PROC_STATE_TRANSMIT_COMPLETE);
            }

            /**
            * Helper Function: Distribute error codes to
            * handling functions, if they exist.
            */
            void SerialError(byte errCode)
            {
                if (ReceiveDataError != null)
                {
                    ReceiveDataError(errCode);
                }
            }

            /* Receiving Variables */
            internal byte [] receive_data = new byte [SerialPacket.SERIAL_RECEIVE_BUFFER_SIZE];
            internal byte receive_data_count; /* Number of bytes received so far */
            internal byte receive_length; /* Expected number of bytes to receive */
            internal bool receive_next_char_is_escaped; /* need to unstuff next char? */
            internal byte receive_address; /* address of the received packet */
            internal byte receive_checksum; /* Checksum of the received packet */
            internal byte receive_state; /* Serial receive state variable */

            /* Transmission Variables */
            internal byte[] transmit_data = new byte[SerialPacket.SERIAL_TRANSMIT_BUFFER_SIZE];
            internal byte transmit_state;
            internal byte transmit_address;
            internal byte transmit_length;
            internal byte transmit_checksum;
            internal byte transmit_escaped_char;
            internal byte transmit_data_index;

            /* Function Pointers */
            public TransmitDelegate Transmit;
            public TransmitPacketeCompleteDelegate TransmitPacketComplete;
            public ReceivePacketCOmpleteDelegate ReceivePacketComplete;
            public ReceiveDataErrorDelegate ReceiveDataError;

            
            //void (*Transmit)(byte data);
            //void (*TransmitPacketComplete)(void);
            //void (*ReceivePacketComplete)(volatile SerialData * s);
            //void (*ReceiveDataError)(byte errCode);
        //}

       
                
        /* Control Characters and thier Escaped Equivelants */
        const byte START_BYTE = 0xCA;
        const byte ESCAPE_CHAR = (byte)'\\';
        const byte null_BYTE = 0;
        const byte MAX_BYTE = 255;
        const byte START_BYTE_ESCAPED = 2;
        const byte ESCAPE_CHAR_ESCAPED = 3;
        const byte null_BYTE_ESCAPED = 4;
        const byte MAX_BYTE_ESCAPED = 5;

        /* Receive State Machine States */
        const byte PROC_STATE_AWAITING_START_BYTE = 0;
        const byte PROC_STATE_AWAITING_ADDRESS = 1;
        const byte PROC_STATE_AWAITING_LENGTH = 2;
        const byte PROC_STATE_AWAITING_DATA = 3;
        const byte PROC_STATE_AWAITING_CHECKSUM = 4;

        /* Transmit State Machine States */
        const byte PROC_STATE_TRANSMIT_ADDRESS = 15;
        const byte PROC_STATE_TRANSMIT_LENGTH = 16;
        const byte PROC_STATE_TRANSMIT_DATA = 17;
        const byte PROC_STATE_TRANSMIT_CHECKSUM = 18;
        const byte PROC_STATE_TRANSMIT_ALMOST_COMPLETE = 19;
        const byte PROC_STATE_TRANSMIT_COMPLETE = 20;

        /* Error Codes */
        const byte ERR_START_BYTE_INSIDE_PACKET = 1;
        const byte ERR_UNEXPECTED_START_BYTE = 2;
        const byte ERR_UNKNOWN_ESCAPED_CHARACTER = 3;
        const byte ERR_EXCESSIVE_PACKET_LENGTH = 4;
        const byte ERR_CHECKSUM_MISMATCH = 5;
        const byte ERR_BUFFER_INSUFFICIENT = 6;
        const byte ERR_RECEIVED_IGNORE_BYTE = 7;

        /* Buffer Sizes */
        const byte SERIAL_RECEIVE_BUFFER_SIZE = 50;
        const byte SERIAL_TRANSMIT_BUFFER_SIZE = 50;
    }
}
