using System;

namespace StreamAtlas_System
{
    class Program
    {
        static void Main(string[] args)
        {
            // Sets the title of the console window
            Console.Title = "StreamAtlas Host";

            WebServer server = new WebServer();
            server.Start();
        }
    }
}