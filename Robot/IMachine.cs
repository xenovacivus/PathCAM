using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;

namespace Robot
{
    interface IMachine
    {
        // Command Generation
        byte[] GenerateNextCommand();

        // State Modification
        void Zero(); // Replace with "Home?"

        void AddMove(Vector3 location, float inches_per_second);

        void Pause();
        void Resume();
        void ClearPendingCommands();

        void EnableMotors();
        void DisableMotors();

        bool ProcessByte(byte[] b);

        // Machine Status
        bool IsPaused { get; }
        bool CanAcceptMove { get; }
        bool IsIdle { get; }

        Vector3 GetPosition();
    }
}
