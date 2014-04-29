using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Robot
{
    abstract class ICommandGenerator
    {
        public abstract IRobotCommand GenerateMoveCommand(Vector3 location, float inches_per_second);
        public abstract IRobotCommand GenerateStatusCommand();
        public abstract IRobotCommand GenerateResetCommand();
        public abstract IRobotCommand GenerateZeroCommand();
        public abstract IRobotCommand GeneratePauseCommand();
        public abstract IRobotCommand GenerateResumeCommand();
        public abstract IRobotCommand GenerateCancelCommand();
        public abstract IRobotCommand GenerateStepperEnableCommand();
        public abstract IRobotCommand GenerateStepperDisableCommand();
    }
}
