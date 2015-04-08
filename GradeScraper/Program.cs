using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace GradeScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Username: ");

            string username = Console.ReadLine();

            Console.Write("Password: ");
            string password = GetConsolePassword();

            Task<Report> report = Browser.GetGradeReport(username, password);
            report.Wait();

            report.Result.PrettyPrint();

            Console.Read();
        }
        
        private static string GetConsolePassword()
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        Console.Write("\b \b");
                        sb.Length--;
                    }

                    continue;
                }

                Console.Write("*");
                sb.Append(key.KeyChar);
            }

            return sb.ToString();
        }
    }
}
