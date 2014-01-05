using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace Commands
{
    public class MoveTool : ICommand
    {
        private Vector3 target; // Target location in inches
        private float speed;    // Moving speed in inches per minute

        public MoveTool(Vector3 target, float speed)
        {
            this.target = target;
            this.speed = speed;
        }

        public Vector3 Target
        {
            get { return target; }
        }

        public float Speed 
        {
            get { return speed; } 
        }
    }
}
