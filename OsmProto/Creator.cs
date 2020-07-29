using System;
using System.Collections.Generic;
using System.Text;
using TruckLib.ScsMap;

namespace OsmProto
{
    abstract class Creator
    {
        protected Map map;
        protected IProjection projection;
        protected Elevation elevation;

        protected ushort ViewDistance = 1400;

        public event EventHandler<float> ProgressChanged;
        public event EventHandler<string> ProgressMessageChanged;

        public Creator(Map map, IProjection projection, Elevation elevation)
        {
            this.map = map;
            this.projection = projection;
            this.elevation = elevation;
        }

        protected void UpdateProgress(float progress)
        {
            ProgressChanged?.Invoke(this, progress);
        }

        protected void UpdateProgressMessage(string message)
        {
            ProgressMessageChanged?.Invoke(this, message);
        }
    }
}
