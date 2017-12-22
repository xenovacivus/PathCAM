using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot
{
    partial class PacketizedCommandGenerator : ICommandGenerator
    {
        #region Serial Packetizer and Depacketize State Machines

        private class SerialPacket
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


            /* Control Characters and thier Escaped Equivelants */
            const byte START_BYTE = 0xCA;
            const byte ESCAPE_CHAR = (byte)'\\';
            const byte null_BYTE = 0;
            const byte MAX_BYTE = 255;
            const byte START_BYTE_ESCAPED = 2;
            const byte ESCAPE_CHAR_ESCAPED = 3;
            const byte null_BYTE_ESCAPED = 4;
            const byte MAX_BYTE_ESCAPED = 5;


            private enum ReceiveState
            {
                AwaitingStartByte,
                AwaitingAddress,
                AwaitingLength,
                AwaitingData,
                AwaitingChecksum,
            }

            ReceiveState receive_state;
            bool receive_next_char_is_escaped;
            byte receive_address;
            byte receive_checksum;
            byte receive_length;
            List<byte> receive_data;
            public SerialPacket()
            {
                receive_state = ReceiveState.AwaitingStartByte;
                receive_next_char_is_escaped = false;
                receive_address = 0;
                receive_checksum = 0;
                receive_length = 0;
                receive_data = new List<byte>();
            }

            /// <summary>
            /// Feed a byte to the serial depacketizing state machine
            /// </summary>
            /// <param name="data"></param>
            /// <returns>non-null byte [] when a packet is received completely</returns>
            public byte[] ProcessByte(byte data)
            {
                // Check for invalid data conditions, ignore any bad bytes
                if (data == null_BYTE || data == MAX_BYTE)
                {
                    Console.WriteLine("Serial Error: Got bad byte from serial stream");
                    return null;
                }

                if (receive_state == ReceiveState.AwaitingStartByte && data != START_BYTE)
                {
                    Console.WriteLine("Serial Error: Got an unexpected byte while waiting for start byte");
                    return null;
                }

                if (receive_state != ReceiveState.AwaitingStartByte && data == START_BYTE)
                {
                    Console.WriteLine("Serial Error: start byte found while already processing a packet");
                    return null;
                }

                // Check if the next byte is escaped
                if (data == ESCAPE_CHAR)
                {
                    receive_next_char_is_escaped = true;
                    return null;
                }
                if (receive_next_char_is_escaped)
                {
                    if (data == ESCAPE_CHAR_ESCAPED) {
                        data =  ESCAPE_CHAR;
                    } else if (data == START_BYTE_ESCAPED) {
                               data =  START_BYTE;
                    } else if (data == null_BYTE_ESCAPED) {
                               data =  null_BYTE;
                    } else if (data == MAX_BYTE_ESCAPED) {
                               data =  MAX_BYTE;
                    }
                    receive_next_char_is_escaped = false;
                }

                switch (receive_state)
                {
                    case ReceiveState.AwaitingStartByte:
                        receive_next_char_is_escaped = false;
                        receive_state = ReceiveState.AwaitingAddress;
                        break;
                    
                    case ReceiveState.AwaitingAddress:
                        receive_address = data;
                        receive_checksum = data;
                        receive_state = ReceiveState.AwaitingLength;
                        break;
                    
                    case ReceiveState.AwaitingLength:
                        receive_length = data;
                        receive_checksum += data;
                        receive_data.Clear();
                        receive_state = ReceiveState.AwaitingData;
                        break;
                    
                    case ReceiveState.AwaitingData:
                        receive_checksum += data;
                        receive_data.Add(data);
                        if (--receive_length == 0)
                        {
                            receive_state = ReceiveState.AwaitingChecksum;
                        }
                        break;
                    
                    case ReceiveState.AwaitingChecksum:
                        receive_state = ReceiveState.AwaitingStartByte;
                        receive_checksum = (byte)~receive_checksum;
                        if (data != receive_checksum)
                        {
                            Console.WriteLine("Serial Error: Checksum Mismatch");
                            return null;
                        }
                        return receive_data.ToArray();
                }
                return null;
            }



            public static byte[] Packetize(byte[] data, byte address)
            {
                byte length = (byte)data.Length;
                List<byte> packet = new List<byte>();

                packet.Add(START_BYTE);
                packet.AddRange(Escape(address));
                packet.AddRange(Escape(length));

                byte checksum = (byte)(length + address);
                foreach (byte b in data)
                {
                    checksum += b;
                    packet.AddRange(Escape(b));
                }
                packet.AddRange(Escape((byte)~checksum));

                return packet.ToArray();
            }

            private static byte[] Escape(byte data)
            {
                switch (data)
                {
                    case START_BYTE:
                        return new byte[] { ESCAPE_CHAR, START_BYTE_ESCAPED };
                    case ESCAPE_CHAR:
                        return new byte[] { ESCAPE_CHAR, ESCAPE_CHAR_ESCAPED };
                    case null_BYTE:
                        return new byte[] { ESCAPE_CHAR, null_BYTE_ESCAPED };
                    case MAX_BYTE:
                        return new byte[] { ESCAPE_CHAR, MAX_BYTE_ESCAPED };
                    default:
                        return new byte[] { data };
                }
            }
        }

        #endregion


        #region Command Definitions

        private class StatusCommand : IRobotCommandWithStatus
        {
            private SerialPacket depacketizer = new SerialPacket();
            private Vector3 currentPosition = new Vector3(0, 0, 0);
            private bool data_valid = false;
            //private bool is_moving = false;
            private int locations;

            // TODO: find a better way to handle the time
            private float time;

            public override float Time { get { return time; } }
            

            public StatusCommand()
            {
            }

            protected virtual byte CommandCode
            {
                get { return 0x77; }
            }

            public override bool CanAcceptMoveCommand
            {
                get { return locations > 0; }
            }

            public override Vector3 CurrentPosition
            {
                get { return currentPosition; }
            }

            //internal override bool IsDataValid()
            //{
            //    return data_valid;
            //}

            //internal bool IsMoving()
            //{
            //    return is_moving;
            //}

            internal override byte[] GenerateCommand()
            {
                return SerialPacket.Packetize(new byte[] { CommandCode }, 0x21);
            }

            bool paused = false;
            bool pausing = false;
            bool steppers_enabled = false;
            public override bool Homed { get { return true; } }
            public override bool Paused { get { return paused; } }
            public override bool Pausing { get { return pausing; } }
            public override bool SteppersEnabled { get { return steppers_enabled; } }

            List<byte> data = new List<byte>();
            internal override bool ProcessResponse(byte b)
            {
                byte[] data = depacketizer.ProcessByte(b);
                if (data == null)
                {
                    return false;
                }

                // TODO: need to depacketize the data...
                //data.Add(b);
                //if (data.Count <= 18)
                //{
                //    return false;
                //}
                data_valid = (data[0] == CommandCode);

                List<byte> data_list = new List<byte>(data);

                time = ((float)DataConverter.IntFromBytes(data_list.GetRange(15, 4))) / 10.0f;
                currentPosition = new Vector3(
                    DataConverter.FloatFromBytes(data_list.GetRange(1, 4)),
                    DataConverter.FloatFromBytes(data_list.GetRange(5, 4)),
                    DataConverter.FloatFromBytes(data_list.GetRange(9, 4)));

                byte status_bits = data[13];
                paused = (status_bits & 0x01) > 0;
                pausing = (status_bits & 0x02) > 0;
                steppers_enabled = (status_bits & 0x04) > 0;

                //is_moving = true;// x_moving | y_moving | z_moving;

                locations = (int)(data[14]);
                return true;
                //Console.WriteLine("{0}, {1}, {2}, {3}, l = {4}, paused = {5}, pausing = {6}, resuming = {7}", time, currentPosition.X, currentPosition.Y, currentPosition.Z, locations, paused, pausing, resuming);
            }

        }

        private class MoveCommand : StatusCommand
        {
            protected override byte CommandCode
            {
                get { return 0x32; }
            }

            private UInt16 thousandths_per_second;
            private Vector3 toLocation = new Vector3(0, 0, 0);

            public MoveCommand(Vector3 location, float inches_per_second)
                : base()
            {
                toLocation = location;
                this.thousandths_per_second = (UInt16)(inches_per_second * 1000);
            }

            internal override byte[] GenerateCommand()
            {
                List<byte> command = new List<byte>();
                command.Add(CommandCode);
                command.AddRange(DataConverter.BytesFromShort((short)thousandths_per_second));
                command.AddRange(DataConverter.BytesFromFloat(toLocation.X));
                command.AddRange(DataConverter.BytesFromFloat(toLocation.Y));
                command.AddRange(DataConverter.BytesFromFloat(toLocation.Z));
                return SerialPacket.Packetize(command.ToArray(), 0x21);
            }
        }

        private abstract class SingleByteStatusCommand : StatusCommand
        {
            internal override byte[] GenerateCommand()
            {
                return SerialPacket.Packetize(new byte[] { CommandCode }, 0x21);
            }
        }

        private class StepperDisableCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x14; } }
            public StepperDisableCommand() : base() { }
        }

        private class StepperEnableCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x15; } }
            public StepperEnableCommand() : base() { }
        }

        private class CancelCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x13; } }
            public CancelCommand() : base() { }
        }

        private class PauseCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x11; } }
            public PauseCommand() : base() { }
        }

        private class ResumeCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x12; } }
            public ResumeCommand() : base() { }
        }

        private class ResetCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x88; } }
            public ResetCommand() : base() { }
        }

        private class ZeroCommand : SingleByteStatusCommand
        {
            protected override byte CommandCode { get { return 0x90; } }
            public ZeroCommand() : base() { }
        }

        #endregion


        #region ICommandGenerator Implementation

        public override IRobotCommand GetNextSetupCommand()
        {
            return null;
        }

        public override IRobotCommand GenerateMoveCommand(Vector3 location, float inches_per_second)
        {
            return new MoveCommand(location, inches_per_second);
        }

        public override IRobotCommand GenerateStatusCommand()
        {
            return new StatusCommand();
        }

        public override IRobotCommand GenerateResetCommand()
        {
            return new ResetCommand();
        }

        public override IRobotCommand GenerateZeroCommand()
        {
            return new ZeroCommand();
        }

        public override IRobotCommand GeneratePauseCommand()
        {
            return new PauseCommand();
        }

        public override IRobotCommand GenerateResumeCommand()
        {
            return new ResumeCommand();
        }

        public override IRobotCommand GenerateCancelCommand()
        {
            return new CancelCommand();
        }

        public override IRobotCommand GenerateStepperEnableCommand()
        {
            return new StepperEnableCommand();
        }

        public override IRobotCommand GenerateStepperDisableCommand()
        {
            return new StepperDisableCommand();
        }

        #endregion


        public override IRobotCommand GenerateSpindleEnableCommand(float spindleRPM)
        {
            Console.WriteLine("Robot does not support spindle enable command");
            return new StatusCommand();
            //throw new NotImplementedException();
        }

        public override IRobotCommand GenerateSpindleDisableCommand()
        {
            return new StatusCommand();
            //throw new NotImplementedException();
        }
    }
}
