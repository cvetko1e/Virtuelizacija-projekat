using System;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (ServiceHost host = new ServiceHost(typeof(WeatherService)))
            {
                host.Open();
                Console.WriteLine("WeatherService je pokrenut.");
                Console.WriteLine("Pritisnite ENTER za gasenje servisa...");
                Console.ReadLine();
                host.Close();
            }
        }
    }
}
