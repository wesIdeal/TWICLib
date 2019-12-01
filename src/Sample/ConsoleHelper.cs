using System;
namespace TWICHelper
{
    public enum MultiResponse
    {
        No, Yes, All, unknown
    }

    public static class ConsoleHelper
    {
        public static MultiResponse GetContinueMultiResponse()
        {
            Console.WriteLine("[y]es | [n]o | [a]ll");
            var key = Char.ToLower(Console.ReadKey().KeyChar);
            switch (key)
            {
                case 'n': return MultiResponse.No;
                case 'y': return MultiResponse.Yes;
                case 'a': return MultiResponse.All;
                default: return MultiResponse.unknown;
            }
        }
        public static bool GetContinue()
        {
            var key = Char.ToLower(Console.ReadKey().KeyChar);
            Console.WriteLine();
            return key == 'y';
        }
    }
}
