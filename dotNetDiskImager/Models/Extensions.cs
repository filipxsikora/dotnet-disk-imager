using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public static class Extensions
    {
        public static void Fill(this byte[] arr, byte value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }

        public static ulong Sum(this List<ulong> list)
        {
            ulong sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                sum += list[i];
            }

            return sum;
        }
    }
}
