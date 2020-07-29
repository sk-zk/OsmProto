using System;
using System.Collections.Generic;
using System.Text;

namespace OsmProto
{
    public struct BBox<T>
    {
        public T Min;
        public T Max;

        public BBox(T min, T max)
        {
            Min = min;
            Max = max;
        }
    }
}
