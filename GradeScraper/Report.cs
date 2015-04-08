using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GradeScraper
{
    class Report
    {
        public Report(List<Course> courses)
        {
            this.Courses = courses;
        }

        /*
         * Only really prints the overall grades, and doesn't show off the full functionality of the
         * scraper -- doesn't show all the assignments or anything.
         */
        public void PrettyPrint()
        {
            Console.WriteLine();
            Console.WriteLine(String.Format("{0,20} {1,20} {2,7}    {3,15} ", "Course Name", "Teacher", "Grade", "Term"));
            Console.WriteLine();

            foreach (var course in this.Courses)
            {
                Console.WriteLine(String.Format("{0,20} {1,20} {2,7}    {3,15} ", course.Name, course.Teacher, course.OverallGrade, course.Term));
            }
        }

        public IEnumerable<Course> Courses { get; private set; }
    }

    class Course
    {
        public Course(string name, string overallGrade, string term, string teacher, List<Assignment> assignments)
        {
            this.Name = name;
            this.OverallGrade = overallGrade;
            this.Term = term;
            this.Teacher = teacher;
            this.Assignments = assignments;
        }

        public string Name { get; private set; }
        public string OverallGrade { get; private set; }
        public string Term { get; private set; }
        public string Teacher { get; private set; }
        public IEnumerable<Assignment> Assignments { get; private set; }
    }

    class Assignment
    {
        public Assignment(int number, string description, string dueDate, string category, string grade, string max, string letter, string comments)
        {
            this.Number = number;
            this.Description = description;
            this.DueDate = dueDate;
            this.Category = category;
            this.Grade = grade;
            this.Max = max;
            this.Letter = letter;
            this.Comments = comments;
        }

        public int Number { get; private set; }
        public string Description { get; private set; }
        public string DueDate { get; private set; }
        public string Category { get; private set; }
        public string Grade { get; private set; }
        public string Max { get; private set; }
        public string Letter { get; private set; }
        public string Comments { get; private set; }
    }
}
