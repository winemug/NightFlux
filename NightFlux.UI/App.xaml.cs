using System;
using System.Windows;

namespace NightFlux.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {

        public static Configuration Configuration;
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                if (e.Args.Length > 0)
                    Configuration = Configuration.Load(e.Args[0]);
                else
                    Configuration = Configuration.Load();

            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error reading configuration:\n{ex}");
                throw;
            }
        }
    }
}
