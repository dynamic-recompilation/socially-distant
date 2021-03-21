﻿using System.Linq;

namespace RedTeam.Commands
{
    public class Cat : Command
    {
        public override string Name => "cat";

        protected override void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("{0}: usage: {0} <path>", Name);
                return;
            }

            var path = ResolvePath(args.First());

            if (FileSystem.DirectoryExists(path))
            {
                Console.WriteLine("{0}: {1}: Is a directory.", Name, path);
            }
            else if (FileSystem.FileExists(path))
            {
                Console.WriteLine(FileSystem.ReadAllText(path));
            }
            else
            {
                Console.WriteLine("{0}: {1}: File not found.", Name, path);
            }
        }
    }
}