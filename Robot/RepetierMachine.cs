using OpenTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Robot
{
    class RepetierMachine : IMachine
    {
        private StringBuilder dataBuffer;
        private Vector3 lastStatusResponsePosition = Vector3.Zero;
        
        // For timing how long a status response takes.
        //Stopwatch statusResponseTimer;

        private Stopwatch statusHoldoffTimer;
        public RepetierMachine()
        {
            // This timer will be started when a status response comes in.
            // The total status query period will be status request to response time
            // (about 6 milliseconds: 3.47ms for the serial communication @115200 baud and a few for the OS side of things)
            // plus however long the timer is set for (plus the granularity of calls to GenerateNextCommand from Robot).
            // Current settings get a new status between 30 and 50 milliseconds.
            statusHoldoffTimer = new Stopwatch();
            statusHoldoffTimer.Reset();
            statusHoldoffTimer.Start();

            //statusResponseTimer = new Stopwatch();
            dataBuffer = new StringBuilder();
        }
        string resend_command = "";

        Stopwatch commandTimer = new Stopwatch();
        /// <summary>
        /// Process a byte coming from the GRBL Machine.
        /// Returns true if there's a relevant state change,
        /// false otherwise
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        bool IMachine.ProcessByte(byte[] b)
        {
            string newData = System.Text.Encoding.ASCII.GetString(b);
            bool relevantStateChange = false;
            dataBuffer.Append(newData);
            if (newData.Contains('\n'))
            {
                string allData = dataBuffer.ToString();
                List<string>lines = new List<string>(allData.Split('\n'));

                // The last part is still being built.
                dataBuffer = new StringBuilder(lines.Last());
                lines.RemoveAt(lines.Count - 1);

                foreach (string response in lines)
                {
                    string number_regex = @"[+-]?[0-9\.]+";

                    // Repetier position response:
                    //X: 0.00 Y: 0.00 Z: 0.00 E: 0.00
                    Regex positionResponseRegex = new Regex(@"\s*" +
                        @"X:\s*" + @"(?<x_pos>" + number_regex + @")\s*" +
                        @"Y:\s*" + @"(?<y_pos>" + number_regex + @")\s*" +
                        @"Z:\s*" + @"(?<z_pos>" + number_regex + @")\s*"
                        );

                    // Repetier status response
                    Regex statusResponseRegex = new Regex(@"\s*" +
                        @"(?<type>[a-zA-Z]+)[^0-9]+" +
                        @"(?<line_number>[0-9]+).*");

                    Console.WriteLine("[" + commandTimer.ElapsedMilliseconds + "] " + "Response from Repetier Machine: '" + response.TrimEnd() + "'");
                    Match statusResponseMatch = statusResponseRegex.Match(response);
                    Match statusMessageMatch = positionResponseRegex.Match(response);
                    if (statusMessageMatch.Success)
                    {
                        if (float.TryParse(statusMessageMatch.Groups["x_pos"].Value, out float x_pos) &&
                            float.TryParse(statusMessageMatch.Groups["y_pos"].Value, out float y_pos) &&
                            float.TryParse(statusMessageMatch.Groups["z_pos"].Value, out float z_pos))
                        {
                            Vector3 newPosition = new Vector3(x_pos, y_pos, z_pos);
                            newPosition /= 25.4f;
                            lastStatusResponsePosition = newPosition;
                            //Console.WriteLine("Parsed status OK: " + newPosition + " " + response);
                            relevantStateChange = true;
                            pendingStatusResponse = false;
                            statusHoldoffTimer.Reset();
                            statusHoldoffTimer.Start();
                        }
                    }
                    else if (statusResponseMatch.Success)
                    {
                        string type = statusResponseMatch.Groups["type"].Value;
                        if (int.TryParse(statusResponseMatch.Groups["line_number"].Value, out int line))
                        {
                            if (type == "ok")
                            {
                                if (pendingCommands.ContainsKey(line))
                                {
                                    var item = pendingCommands[line];
                                    if (item.hasPosition)
                                    {
                                        lastStatusResponsePosition = item.position;
                                        relevantStateChange = true;
                                    }
                                    pendingCommands.Remove(line);
                                }
                                //Console.WriteLine("Confirmed line number " + line + ": " + response.Trim() +
                                //    "(" + pendingCommands.Count + " pending commands)");
                            }
                            else if (type == "Resend" || type == "rs")
                            {
                                Console.WriteLine("Need to resend line number " + line);
                                if (pendingCommands.ContainsKey(line))
                                {
                                    resend_command = pendingCommands[line].command;
                                }
                                else
                                {
                                    Console.WriteLine("Cannot resend line number " + line + ", does not exist in pending command dict");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Unknown response type: " + type.Trim() + " for line " + line);
                            }
                        }

                        //Console.WriteLine("StatusResponseRegex Match: " +
                        //    statusResponseMatch.Groups["type"].Value +
                        //statusResponseMatch.Groups["line_number"].Value);
                    }
                    else if (response.StartsWith("wait"))
                    {
                        Console.WriteLine("Received wait, had " + pendingCommands.Count + " commands with no responses");
                        pendingCommands.Clear();
                    }
                    else
                    {
                        Console.WriteLine("Command Not Understood: '" + response.Trim() + "'");
                    }
                }
            }
            return relevantStateChange;
        }

        
        bool isPaused = false;
        bool IMachine.IsPaused
        {
            get { return isPaused; }
        }
        bool IMachine.CanAcceptMove
        {
            get { return !hasPendingMove; }
        }

        bool isIdle = true;
        public bool IsIdle
        {
            get { return isIdle; }
        }

        bool sendClearCommand = false;
        void IMachine.ClearPendingCommands()
        {
            lastMoveInchesPerSecond = -1.0f;
            sendClearCommand = true;
        }

        void IMachine.DisableMotors()
        {
        }

        void IMachine.EnableMotors()
        {
        }

        Vector3 IMachine.GetPosition()
        {
            return lastStatusResponsePosition;
        }

        bool sendPauseCommand = false;
        void IMachine.Pause()
        {
            if (!sendPauseCommand)
            {
                sendPauseCommand = true;
            }
        }

        bool sendResumeCommand = false;
        void IMachine.Resume()
        {
            if (!sendResumeCommand)
            {
                sendResumeCommand = true;
            }
        }

        bool sendHomeCommand = false;
        void IMachine.Zero()
        {
            if (!sendHomeCommand)
            {
                sendHomeCommand = true;
            }
        }

        bool hasPendingMove = false;
        Vector3 pendingMoveTarget;
        //bool hasPendingSpeedChange = false;
        float pendingMoveInchesPerSecond;

        bool lastMoveAcknowleged = true;


        // Initial state, if disconnected and reconnected put these
        // back to original values.  Currently every reconnect creates
        // a new RepetierMachine object though.

        // Commands sent without line numbers seem to not advance the line number
        // counter.
        int line_number = 2; // The command number after M110
        List<string> initCommands = new List<string>() { "M110 N1\n", "G21\n" };

        private class PendingCommand
        {
            public string command;
            public bool hasPosition;
            public Vector3 position;
        }

        Dictionary<int, PendingCommand> pendingCommands = new Dictionary<int, PendingCommand>();

        private string AddLineNumberAndChecksum(string command)
        {
            return AddLineNumberAndChecksum(command, Vector3.Zero, false);
        }
        private string AddLineNumberAndChecksum(string command, Vector3 position, bool hasPosition = true)
        {
            string s = "N" + line_number + " " + command;
            PendingCommand pc = new PendingCommand();
            pc.command = s;
            pc.position = position;
            pc.hasPosition = hasPosition;
            pendingCommands[line_number] = pc;
            line_number = line_number + 1;
            return s;
        }

        byte[] IMachine.GenerateNextCommand()
        {
            if (!commandTimer.IsRunning)
            {
                commandTimer.Reset();
                commandTimer.Start();
            }

            string s = "";

            if (initCommands.Count > 0)
            {
                s = initCommands.First();
                initCommands.RemoveAt(0);
            }
            else
            {
                if (resend_command != "")
                {
                    Console.WriteLine("Resending command " + resend_command);
                    s = resend_command;
                    resend_command = "";
                }
                else if (pendingCommands.Count == 0)
                {
                    if (hasPendingMove)
                    {
                            var target_mm = pendingMoveTarget * 25.4f;
                            float target_mm_per_minute = pendingMoveInchesPerSecond * 25.4f * 60.0f;
                            s += AddLineNumberAndChecksum(string.Format("G1 F{0:F3} X{1:F4} Y{2:F4} Z{3:F4}\n", target_mm_per_minute,
                                target_mm.X, target_mm.Y, target_mm.Z), pendingMoveTarget);
                            hasPendingMove = false;   
                    }
                    else if(statusHoldoffTimer.ElapsedMilliseconds >= 1000)
                    {
                        s += AddLineNumberAndChecksum("M114\n");
                    }
                }
            }
            foreach (string line in s.Split('\n'))
            {
                if (line != "")
                {
                    Console.WriteLine("[" + commandTimer.ElapsedMilliseconds + "] " + "Sending Command: " + line);
                }
            }

            return System.Text.Encoding.ASCII.GetBytes(s);
        }

        private void ResetState()
        {
            sendClearCommand = false;
            sendHomeCommand = false;
            sendPauseCommand = false;
            sendResumeCommand = false;
            pendingStatusResponse = false;
            hasPendingMove = false;
            lastMoveInchesPerSecond = -1;
            lastMoveAcknowleged = true;
            // Maintain the last status response position though.
        }

        bool pendingStatusResponse = false;


        float lastMoveInchesPerSecond = -1.0f;
        void IMachine.AddMove(Vector3 location, float inches_per_second)
        {
            if (inches_per_second != lastMoveInchesPerSecond)
            {
                lastMoveInchesPerSecond = inches_per_second;
                pendingMoveInchesPerSecond = inches_per_second;
            }
            
            pendingMoveTarget = location;
            hasPendingMove = true;
            Console.WriteLine("[" + commandTimer.ElapsedMilliseconds + "] " + "Add Pending Move");
        }
    }
}
