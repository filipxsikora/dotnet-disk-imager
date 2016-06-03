using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotNetDiskImager.Models
{
    public class CommandLineArguments
    {
        public bool AutoStart { get; set; } = false;
        public bool ReadOnlyAllocated { get; set; } = false;
        public string ImagePath { get; set; } = "";
        public List<char> Devices { get; set; } = new List<char>();
        public bool Verify { get; set; } = false;
        public bool Read { get; set; } = false;
        public bool Write { get; set; } = false;
        public bool? Zip { get; set; } = null;

        public static CommandLineArguments Parse(string[] args)
        {
            CommandLineArguments arguments = new CommandLineArguments();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-')
                {
                    switch (args[i])
                    {
                        case "-d":
                        case "-device":
                            for (int x = i + 1; x < args.Length; x++)
                            {
                                if (args[x][0] == '-')
                                {
                                    break;
                                }
                                arguments.Devices.Add(args[x][0]);
                            }
                            Console.WriteLine();
                            break;
                        case "-i":
                        case "-image":
                            if (i + 1 < args.Length)
                            {
                                if (args[i + 1][0] != '-')
                                {
                                    arguments.ImagePath = args[i + 1];
                                }
                            }
                            break;
                        case "-v":
                        case "-verify":
                            arguments.Verify = true;
                            break;
                        case "-w":
                        case "-write":
                            arguments.Write = true;
                            break;
                        case "-r":
                        case "-read":
                            arguments.Read = true;
                            break;
                        case "-oa":
                        case "-onlyallocated":
                            arguments.ReadOnlyAllocated = true;
                            break;
                        case "-z":
                        case "-zip":
                            if (i + 1 < args.Length)
                            {
                                if (args[i + 1].ToLower() == "on")
                                {
                                    arguments.Zip = true;
                                }
                                else if (args[i + 1].ToLower() == "off")
                                {
                                    arguments.Zip = false;
                                }
                            }
                            break;
                        case "-s":
                        case "-start":
                            arguments.AutoStart = true;
                            break;
                    }
                }
            }

            return arguments;
        }
    }
}
