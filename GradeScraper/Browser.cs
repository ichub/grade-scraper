using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace GradeScraper
{
    static class Browser
    {
        private const string LoginUrl = "https://grades.bsd405.org/Pinnacle/Gradebook/Logon.aspx?ReturnUrl=%2fPinnacle%2fGradebook";
        private const string GradeSummaryUrl = "https://grades.bsd405.org/Pinnacle/Gradebook/InternetViewer/gradesummary.aspx";
        private const string GradeRootUrl = "https://grades.bsd405.org/Pinnacle/Gradebook/InternetViewer/";

        public static async Task<Report> GetGradeReport(string username, string password)
        {
            using (HttpClient client = new HttpClient())
            {
                InitClient(client);

                var validationValues = await ScrapeFormValidationValues(client);

                await Login(username, password, client, validationValues);

                return await ScrapeAllCourses(client);
            }
        }

        /*
        * For some reason, the grade server gives an empty response unless we have a user agent, so we set the useragent to be the latest version of chrome.
        */
        private static void InitClient(HttpClient client)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.118 Safari/537.36");
        }

        /*
         * Typically, to log in, a user inputs some values into a form, and then the form is submitted to the server, and the
         * server responds by redirecting the user to the desired page, as well as setting a cookie in the browser so that 
         * the user does not have to log in again. The "HttpClient" class takes care of storing the cookie, so that after
         * we log in once, we never have to log in again (unless the program is restarted). This method takes care of logging 
         * in using the supplied credentials, as well as some hidden form parameters that should have been scraped earlier.
         */
        public static async Task Login(string username, string password, HttpClient client, Dictionary<string, string> variables)
        {
            // init of all the form values, even the required ones.
            // the ones we don't have a value for are left empty
            var formValues = new Dictionary<string, string>
                {
                   { "__LASTFOCUS", "" },
                   { "__EVENTTARGET", "" },
                   { "__EVENTARGUMENT", "" },
                   { "__VIEWSTATE", "" },
                   { "__VIEWSTATEGENERATOR", "" },
                   { "__EVENTVALIDATION", "" },
                   { "ctl00$ContentPlaceHolder$Username", username },
                   { "ctl00$ContentPlaceHolder$Password", password },
                   { "ctl00$ContentPlaceHolder$lstDomains", "Pinnacle" },
                   { "ctl00$ContentPlaceHolder$LogonButton", "Sign In" },
                   { "PageUniqueId", "" }
                };

            // place all the scraped variable values into the form
            foreach (var variable in variables)
            {
                formValues[variable.Key] = variable.Value;
            }

            var content = new FormUrlEncodedContent(formValues);

            // post the login form
            await HttpPost(client, LoginUrl, content);
        }

        /*
         * Gets the form validation values from the homepage of the grade viewer site. The login form has special values (eg. __VIEWSTATE
         * without which a login request is denied. These values are generated fresh for every single page view on the server, so to login we have 
         * load a new instance of the page, scrape that information from it, and use the values to fill in the login form
         * The required variables are
         * 1. __VIEWSTATE
         * 2. __EVENTVALIDATION
         * 3. __VIEWSTATEGENERATOR
         * 4. PageUniqueId
         */
        private static async Task<Dictionary<string, string>> ScrapeFormValidationValues(HttpClient client)
        {
            var responseString = await HttpGet(client, LoginUrl);

            string[] requiredVarNames = new[]
            {
                "__VIEWSTATE",
                "__VIEWSTATEGENERATOR",
                "__EVENTVALIDATION",
                "PageUniqueId"
            };

            return requiredVarNames.Aggregate(new Dictionary<string, string>(), (aggregate, name) =>
            {
                aggregate.Add(name, CreateRegexForValidationParameter(name).Match(responseString).Value);

                return aggregate;
            });
        }

        /*
        * All the validation form values are stored in the HTML in the same way, for example:
        * <input type="hidden" name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="105D578B" />
        * This makes scraping them from the html easy by using a regular expression that checks for the opening part,
        * which contains the element name (input) and the parameter name in two places ("name" and "id" properties).
        * So that we don't have to write each regex out by hand, this method generates regular expressions taking into
        * account this pattern.
        */
        private static Regex CreateRegexForValidationParameter(string parameterName)
        {
            string htmlElement = "<input type=\"hidden\" name=\"{0}\" id=\"{0}\" value=\"";
            htmlElement = String.Format(htmlElement, parameterName);

            return new Regex("(?<=" + Regex.Escape(htmlElement) + ").*(?=\" />)");
        }

        private static async Task<Report> ScrapeAllCourses(HttpClient client)
        {
            List<Course> courses = new List<Course>();

            string html = await HttpGet(client, GradeSummaryUrl);

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var courseRows = from row in doc.DocumentNode.Descendants("tr")
                             where row.Attributes.Contains("class")
                             where row.Attributes["class"].Value.Contains("row")
                             select row;

            foreach (var row in courseRows)
            {
                var gradeElement = (from td in row.Descendants("td")
                                    where td.Attributes["class"].Value.Contains("gradeNumeric")
                                    select td).First();

                string courseUrl = GradeRootUrl +
                    WebUtility.HtmlDecode(
                        gradeElement
                        .FirstChild                     // the <a> tag (link)
                        .Attributes["href"].Value);     // link to the grade page

                string courseHtml = await HttpGet(client, courseUrl);

                courses.Add(ScrapeIndividualCourse(courseHtml));
            }

            return new Report(courses);
        }

        /*
         * Things get messy here, but that's just the nature of scraping. Most of the table rows and values can't
         * be queried via id or name or anything like that, so we have to work around that by figuring out where the
         * elements /should/ be relative to each other. This is, again, very messy, but necessary. Also worth 
         * mentioning is the fact that if the layout of the website changes even a teeny bit, nothing will work.
         */
        private static Course ScrapeIndividualCourse(string courseGradeReportHtml)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(courseGradeReportHtml);

            var table = doc.GetElementbyId("Assignments");                  // the table of assignments actually has an id, so we use it here
            var tbody = table.Descendants("tbody").First();                 // table should have one and only one body, so we can safely assume that's the case
            var rows = tbody.ChildNodes.Where(node => node.Name == "tr");   // query all the table rows

            List<Assignment> assignments = new List<Assignment>();

            foreach (var row in rows)
            {
                assignments.Add(ScrapeAssignment(row));
            }

            var courseDetailsTableBody = doc
                .GetElementbyId("ClassTitle")
                .ParentNode
                .ParentNode
                .ParentNode;                    // this is insanely messy, but unfortunately the only way to get to the parent table

            string courseName = doc             // we actually have the id here, so that's nice
                .GetElementbyId("ClassTitle")
                .InnerText;

            string term = courseDetailsTableBody.ChildNodes[3].ChildNodes[3].InnerText;     // going purely off the relative locations of the elements here
            string teacher = courseDetailsTableBody.ChildNodes[5].ChildNodes[3].InnerText;  // here too...

            string overallGrade = table.Descendants("tfoot").First().Descendants("td").ElementAt(1).InnerText;  // again, just off relative locations

            return new Course(courseName, overallGrade, term, teacher, assignments);
        }

        /*
         * This part is actually really simple, as each row contains all the info we need
         * in the (hopefully eternally) correct order.
         */
        private static Assignment ScrapeAssignment(HtmlNode row)
        {
            int number = Int32.Parse(row.Descendants("th").First().InnerText);
            var tds = row.ChildNodes.Where(node => node.Name == "td").ToArray();

            // each cell in each row should have the same order of attributes
            string description = WebUtility.HtmlDecode(tds[0].InnerText);
            string dueDate = WebUtility.HtmlDecode(tds[1].InnerText);
            string category = WebUtility.HtmlDecode(tds[2].InnerText);
            string grade = WebUtility.HtmlDecode(tds[3].InnerText);
            string max = WebUtility.HtmlDecode(tds[4].InnerText);
            string letter = WebUtility.HtmlDecode(tds[5].InnerText);
            string comments = WebUtility.HtmlDecode(tds[6].InnerText);

            return new Assignment(number, description, dueDate, category, grade, max, letter, comments);
        }

        private static async Task<string> HttpGet(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<string> HttpPost(HttpClient client, string url, FormUrlEncodedContent content)
        {
            var response = await client.PostAsync(url, content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
