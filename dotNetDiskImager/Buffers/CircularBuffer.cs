using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Buffers
{
    public class CircularBuffer
    {
        public int Capacity { get; }
        public int CurrentIndex { get; private set; }
        public int ItemsCount { get; private set; }
        public bool IsReady { get { return ItemsCount % Capacity == 0; } }

        ulong[] buffer;

        public CircularBuffer(int capacity)
        {
            buffer = new ulong[capacity];
            Capacity = capacity;
            CurrentIndex = 0;
            ItemsCount = 0;
        }

        public void Add(ulong item)
        {
            buffer[CurrentIndex] = item;
            CurrentIndex = (CurrentIndex + 1) % Capacity;
            ItemsCount++;
        }

        public ulong Average()
        {
            ulong temp = 0;
            int count = (IsReady) ? Capacity : ItemsCount;

            for (int i = 0; i < count; i++)
            {
                temp += buffer[i];
            }

            return temp / (ulong)count;
        }

        public void Reset()
        {
            CurrentIndex = 0;
            ItemsCount = 0;
        }
    }
}
