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
            string password = Console.ReadLine();

            Task<Report> report = Browser.GetGradeReport(username, password);
            report.Wait();

            report.Result.PrettyPrint();

            Console.Read();
        }
    }
}
