using GZipTest.Zip;

namespace GZipTest
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 3)
            {
                Archiver arc = new Archiver(args[1], args[2]);
                return arc.Launch(args[0].ToUpper());
            }
            else
            {
                System.Console.WriteLine("Not enough arguments were given");
                return 1;
            }
        }
    }
}
