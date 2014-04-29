using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot
{
    class GrblCommandGenerator : ICommandGenerator
    {
        private StringBuilder dataBuffer;
        
        public GrblCommandGenerator()
        {
            dataBuffer = new StringBuilder();
        }
        
        // All data from the serial port comes through this function via the GrblStatusCommand (or inherited commands)
        private string ProcessGrblByte(byte b)
        {
            dataBuffer.Append((char)b);

            var response = dataBuffer.ToString();
            if (response.EndsWith(">\r\n"))
            {
                // Found the last character in an unbuffered command
                var start = response.IndexOf('<');
                var unbuffered_command = response.Substring(start, response.Length - (start + 2));
                dataBuffer.Remove(start, dataBuffer.Length - start);
                return unbuffered_command;
            }
            else if (response.EndsWith("\r\n"))
            {
                var buffered_command = response.Substring(0, response.Length - 2);
                dataBuffer = new StringBuilder();
                return buffered_command;
            }

            return null;
        }

        private class GrblStatusCommand : IRobotCommandWithStatus
        {
            private Vector3 location = new Vector3(0, 0, 0);
            private GrblCommandGenerator parent;
            private bool canAcceptMoveCommand = false;
            private bool paused = false;
            private bool pausing = false;

            public GrblStatusCommand(GrblCommandGenerator parent)
            {
                this.parent = parent;
            }

            internal override byte[] GenerateCommand()
            {
                //Console.WriteLine("Sending ?");
                return new byte[] { (byte)'?' };
            }

            internal override bool ProcessResponse(byte data)
            {
                var result = parent.ProcessGrblByte(data);
                if (result != null)
                {
                    Console.WriteLine("Received GRBL Data: " + result);
                    if (result.Equals("ok", StringComparison.OrdinalIgnoreCase))
                    {
                        canAcceptMoveCommand = true;
                    }
                    else if (result.StartsWith("<") && result.EndsWith(">"))
                    {
                        // Status response will look like this:
                        // <Idle,MPos:1.000,1.000,1.000,WPos:1.000,1.000,1.000>
                        // TODO: more robust parsing for the GRBL status string
                        string inside = result.Substring(1, result.Length - 2);
                        bool mpos_found = false;
                        List<float> position = new List<float>();
                        foreach (var s in inside.Split(new char[] { ',', ':' }))
                        {
                            if (mpos_found && position.Count < 3)
                            {
                                position.Add(float.Parse(s));
                            }
                            else if (s.Equals("mpos", StringComparison.OrdinalIgnoreCase))
                            {
                                mpos_found = true;
                            }
                            else if (s.Equals("idle", StringComparison.OrdinalIgnoreCase))
                            {
                                canAcceptMoveCommand = true;
                            }
                            else if (s.Equals("queue", StringComparison.OrdinalIgnoreCase))
                            {
                                paused = true;
                            }
                            else if (s.Equals("hold", StringComparison.OrdinalIgnoreCase))
                            {
                                pausing = true;
                            }
                        }
                        if (position.Count != 3)
                        {
                            return false;
                        }
                        location.X = position[0];
                        location.Y = position[1];
                        location.Z = position[2];
                        location = location / 25.4f;
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Command Not Understood: " + result);
                    }
                }
                return false;
            }

            public override bool Paused
            {
                get { return paused; }
            }

            public override bool Pausing
            {
                get { return pausing; }
            }

            public override bool SteppersEnabled
            {
                get { return true; }
            }

            public override Vector3 CurrentPosition
            {
                get { return location; }
            }

            public override float Time
            {
                get { return 1.0f; }
            }

            public override bool CanAcceptMoveCommand
            {
                get { return canAcceptMoveCommand; }
            }
        }

        private class GrblMoveCommand : GrblStatusCommand
        {
            private float target_mm_per_minute;
            private Vector3 toLocation = new Vector3(0, 0, 0);
            
            public GrblMoveCommand(GrblCommandGenerator parent, Vector3 location, float inches_per_second)
                : base(parent)
            {
                toLocation = location;
                target_mm_per_minute = inches_per_second * 25.4f * 60.0f;
            }

            internal override byte[] GenerateCommand()
            {
                var target_mm = toLocation * 25.4f;
                String s = String.Format("F{0:F3}\r\nG1 X{1:F4} Y{2:F4} Z{3:F4}\r\n?", target_mm_per_minute, target_mm.X, target_mm.Y, target_mm.Z);
                Console.WriteLine("Sending: " + s);
                return System.Text.Encoding.ASCII.GetBytes(s);
            }
        }

        private class GrblPauseCommand : GrblStatusCommand
        {
            public GrblPauseCommand(GrblCommandGenerator parent) : base(parent) { }

            internal override byte[] GenerateCommand()
            {
                return new byte [] {(byte)'!', (byte)'?'};
            }
        }

        private class GrblResumeCommand : GrblStatusCommand
        {
            public GrblResumeCommand(GrblCommandGenerator parent) : base(parent) { }
            
            internal override byte[] GenerateCommand()
            {
                return new byte[] { (byte)'~', (byte)'?' };
            }
        }

        private class GrblCancelCommand : GrblStatusCommand
        {
            public GrblCancelCommand(GrblCommandGenerator parent) : base(parent) { }

            internal override byte[] GenerateCommand()
            {
                return new byte[] { 0x18, (byte)'?' };
            }
        }

        private class GrblHomeCommand : GrblStatusCommand
        {
            public GrblHomeCommand(GrblCommandGenerator parent) : base(parent) { }

            internal override byte[] GenerateCommand()
            {
                return System.Text.Encoding.ASCII.GetBytes("$H\r\n?");
            }
        }

        public override IRobotCommand GenerateMoveCommand(OpenTK.Vector3 location, float inches_per_second)
        {
            return new GrblMoveCommand(this, location, inches_per_second);
        }

        public override IRobotCommand GenerateStatusCommand()
        {
            return new GrblStatusCommand(this);
        }

        public override IRobotCommand GenerateResetCommand()
        {
            //throw new NotImplementedException();
            return new GrblStatusCommand(this);
        }

        public override IRobotCommand GenerateZeroCommand()
        {
            return new GrblHomeCommand(this);
        }

        public override IRobotCommand GeneratePauseCommand()
        {
            return new GrblPauseCommand(this);
        }

        public override IRobotCommand GenerateResumeCommand()
        {
            return new GrblResumeCommand(this);
        }

        public override IRobotCommand GenerateCancelCommand()
        {
            return new GrblCancelCommand(this);
        }

        public override IRobotCommand GenerateStepperEnableCommand()
        {
            //throw new NotImplementedException();
            return new GrblStatusCommand(this);
        }

        public override IRobotCommand GenerateStepperDisableCommand()
        {
            //throw new NotImplementedException();
            return new GrblStatusCommand(this);
        }
    }
}
