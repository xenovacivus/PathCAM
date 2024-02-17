using OpenTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Robot
{
    class GrblMachine : IMachine
    {
        private StringBuilder dataBuffer;
        private Vector3 lastStatusResponsePosition = Vector3.Zero;
        
        // For timing how long a status response takes.
        //Stopwatch statusResponseTimer;

        private Stopwatch statusHoldoffTimer;
        public GrblMachine()
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


                    // GRBL v1.1:  <Idle|MPos:0.000,0.000,0.000|FS:0,0>
                    // GRBL v0.9:  <Idle,MPos:1.000,1.000,1.000,WPos:1.000,1.000,1.000>
                    Regex grblStatusRegex = new Regex(@"<(?<state>\w+).*" +
                        "MPos:" + @"(?<x_pos>" + number_regex + ")" +
                        "," + "(?<y_pos>" + number_regex + ")" +
                        "," + "(?<z_pos>" + number_regex + ")" + "[^>]*>"
                        );

                    Match statusMessageMatch = grblStatusRegex.Match(response);


                    //Console.WriteLine("Response from GRBL Machine: '" + response.TrimEnd() + "'");
                    if (response.ToLower().StartsWith("ok"))
                    {
                        lastMoveAcknowleged = true;
                        relevantStateChange = true;
                    }
                    else if (response.ToLower().StartsWith("error"))
                    {
                        // TODO: indicates a gcode command was malformed.
                        // Probably means the machine is in a bad state.
                        Console.WriteLine("GRBL Command Error: " + response);
                    }
                    else if (statusMessageMatch.Success)
                    {
                        string state = statusMessageMatch.Groups["state"].Value;
                        if (float.TryParse(statusMessageMatch.Groups["x_pos"].Value, out float x_pos) &&
                            float.TryParse(statusMessageMatch.Groups["y_pos"].Value, out float y_pos) &&
                            float.TryParse(statusMessageMatch.Groups["z_pos"].Value, out float z_pos))
                        {
                            //statusResponseTimer.Stop();
                            //Console.WriteLine("Time from status request to response: " + statusResponseTimer.ElapsedMilliseconds + " (ticks: " + statusResponseTimer.ElapsedTicks + ")");

                            Vector3 newPosition = new Vector3(x_pos, y_pos, z_pos);
                            newPosition /= 25.4f;
                            if (lastStatusResponsePosition == newPosition && state == "Hold")
                            {
                                // TODO: better way to detect paused?
                                isPaused = true;
                                isIdle = false;
                            }
                            lastStatusResponsePosition = newPosition;
                            if (state == "Queue")
                            {
                                isPaused = true; // Older versions of GRBL (v0.8?)
                                isIdle = false;
                            }
                            if (state == "Run")
                            {
                                isPaused = false;
                                isIdle = false;
                            }
                            if (state == "Idle")
                            {
                                isPaused = false;
                                isIdle = true;
                            }
                            if (state == "Alarm")
                            {
                                // TODO: handle alarm state.
                            }
                            //Console.WriteLine("Parsed status OK: " + newPosition + ", paused = " + isPaused + " " + response);
                            relevantStateChange = true;
                            pendingStatusResponse = false;
                            statusHoldoffTimer.Reset();
                            statusHoldoffTimer.Start();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Command Not Understood: '" + response + "'");
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
            get { return !hasPendingMove && !hasPendingSpeedChange; }
        }

        bool isIdle = false;
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
        bool hasPendingSpeedChange = false;
        float pendingMoveInchesPerSecond;

        bool lastMoveAcknowleged = true;

        byte[] IMachine.GenerateNextCommand()
        {
            string s = "";

            
            //if (holdoffTimerRunnign)
            //{
            //}
            //else

            // If there are move commands and we can send one,
            // send them as fast as possible.
            // TODO: should there be a delay here?
            //if (!holdoffTimerRunnign)
            //{
            //    // OK to send a status command
            //    s = "?";
            //    statusResponseTimer.Reset();
            //    statusResponseTimer.Start();
            //    holdoffTimerRunnign = true;
            //}
            //else 
            if (lastMoveAcknowleged)
            {
                if (hasPendingSpeedChange)
                {
                    float target_mm_per_minute = pendingMoveInchesPerSecond * 25.4f * 60.0f;
                    s = string.Format("F{0:F3}\n", target_mm_per_minute);
                    //Console.WriteLine("Sending: " + s);
                    //statusRequestTimer.Interval = 1; // 1ms between sending position commands
                    hasPendingSpeedChange = false;
                    lastMoveAcknowleged = false;
                }
                else if (hasPendingMove)
                {
                    var target_mm = pendingMoveTarget * 25.4f;
                    s = string.Format("G1 X{0:F4} Y{1:F4} Z{2:F4}\n", target_mm.X, target_mm.Y, target_mm.Z);
                    //Console.WriteLine("Sending: " + s);
                    //statusRequestTimer.Interval = 1; // 1ms between sending position commands
                    hasPendingMove = false;
                    lastMoveAcknowleged = false;
                }
            }

            // Immediate commands to GRBL (get status, pause, resume)
            if (statusHoldoffTimer.ElapsedMilliseconds >= 30 && !pendingStatusResponse)
            {
                // 10 milliseconds since last status response was received
                s += "?";
                //Console.WriteLine("Time since last status update: " + statusHoldoffTimer.ElapsedMilliseconds + " milliseconds");
                //statusResponseTimer.Reset();
                //statusResponseTimer.Start();
                pendingStatusResponse = true;
            }
            else if (statusHoldoffTimer.ElapsedMilliseconds >= 500)
            {
                // Haven't seen a status update for a while, send another status request
                s += "?";
                statusHoldoffTimer.Reset();
                statusHoldoffTimer.Start();
                //statusResponseTimer.Reset();
                //statusResponseTimer.Start();
                pendingStatusResponse = true;
            }
            if (sendPauseCommand)
            {
                sendPauseCommand = false;
                s += "!";
            }
            if (sendResumeCommand)
            {
                sendResumeCommand = false;
                s += "~";
            }

            if (sendClearCommand)
            {
                // Override everything else
                sendClearCommand = false;
                ResetState();
                return new byte[] { 0x18 }; // CTLR+X
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
            hasPendingSpeedChange = false;
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
                hasPendingSpeedChange = true;
            }
            
            pendingMoveTarget = location;
            hasPendingMove = true;
        }
    }
}
