using System;
namespace TWICHelper
{
    public static class ConsoleHelper
    {
        public static bool GetContinue()
        {
            var key = Char.ToLower(Console.ReadKey().KeyChar);
            return key == 'y';
        }
    }
}
