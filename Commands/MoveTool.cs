using OpenTK;

namespace Commands
{
    public class MoveTool : ICommand
    {
        public enum SpeedType
        {
            Cutting,
            Rapid,
        }

        private Vector3 target; // Target location in inches
        private SpeedType speed;

        public MoveTool(Vector3 target, SpeedType speed)
        {
            this.target = target;
            this.speed = speed;
        }

        public Vector3 Target
        {
            get { return target; }
            set { target = value; }
        }

        public SpeedType Speed
        {
            get { return speed; }
            set { speed = value; }
        }
    }
}
