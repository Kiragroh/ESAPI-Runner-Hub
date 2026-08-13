using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VMS.TPS.Common.Model.API
{
    public sealed class UnauthorizedScriptingAPIAccessException : InvalidOperationException
    {
        public UnauthorizedScriptingAPIAccessException(string memberName)
            : base("Synthetic ESAPI rejected reflection access to " + memberName + ".")
        {
        }
    }

    internal static class ApiAccessGuard
    {
        public static void RequireCompileTimeCallSite(string memberName)
        {
            var reflectionFrame = new StackTrace().GetFrames()
                .Select(frame => frame.GetMethod())
                .Where(method => method != null && method.DeclaringType != null)
                .Any(method =>
                    (method.DeclaringType.Namespace ?? string.Empty).StartsWith("System.Reflection", StringComparison.Ordinal) ||
                    method.DeclaringType == typeof(RuntimeMethodHandle) ||
                    string.Equals(method.DeclaringType.FullName, "System.RuntimeType", StringComparison.Ordinal));
            if (reflectionFrame) throw new UnauthorizedScriptingAPIAccessException(memberName);
        }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class ESAPIScriptAttribute : Attribute
    {
        public bool IsWriteable { get; set; }
    }

    public sealed class Application : IDisposable
    {
        private Application()
        {
            PatientSummaries = new List<PatientSummary>
            {
                new PatientSummary { Id = "SYN-1001", FirstName = "Ada", LastName = "Example" },
                new PatientSummary { Id = "SYN-1002", FirstName = "Linus", LastName = "Sample" }
            };
            CurrentUser = new User { Id = "synthetic-user", Name = "Synthetic User" };
        }

        public IList<PatientSummary> PatientSummaries { get; private set; }

        public User CurrentUser { get; private set; }

        public static Application CreateApplication()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("FAKE_VMS_THROW_CREATE"), "1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Synthetic ESAPI startup failure.");
            }

            return new Application();
        }

        public Patient OpenPatientById(string id)
        {
            if (!string.Equals(id, "SYN-1001", StringComparison.Ordinal))
            {
                return null;
            }

            var patient = new Patient { Id = id };
            var image = new Image { Id = "IMG1" };
            var planStructureSet = new StructureSet { Id = "SS1", Image = image };
            var standaloneStructureSet = new StructureSet { Id = "SS-ONLY", Image = new Image { Id = "IMG2" } };
            patient.StructureSets.Add(planStructureSet);
            patient.StructureSets.Add(standaloneStructureSet);
            var course = new Course { Id = "C1", Patient = patient };
            var plan = new ExternalPlanSetup { Id = "P1", Course = course, StructureSet = planStructureSet };
            course.PlanSetups.Add(plan);
            if (string.Equals(Environment.GetEnvironmentVariable("FAKE_VMS_DUPLICATE_PLAN"), "1", StringComparison.Ordinal))
            {
                course.PlanSetups.Add(new ExternalPlanSetup { Id = "P1", Course = course, StructureSet = planStructureSet });
            }
            course.PlanSums.Add(new PlanSum { Id = "SUM1", Course = course, StructureSet = planStructureSet });
            patient.Courses.Add(course);
            if (string.Equals(Environment.GetEnvironmentVariable("FAKE_VMS_DUPLICATE_PLAN_OTHER_COURSE"), "1", StringComparison.Ordinal))
            {
                var secondImage = new Image { Id = "IMG1" };
                var secondStructureSet = new StructureSet { Id = "SS1", Image = secondImage };
                patient.StructureSets.Add(secondStructureSet);
                var secondCourse = new Course { Id = "C2", Patient = patient };
                secondCourse.PlanSetups.Add(new ExternalPlanSetup
                {
                    Id = "P1", Course = secondCourse, StructureSet = secondStructureSet
                });
                patient.Courses.Add(secondCourse);
            }
            return patient;
        }

        public void ClosePatient()
        {
            var marker = Environment.GetEnvironmentVariable("FAKE_VMS_CLOSE_MARKER");
            if (!string.IsNullOrWhiteSpace(marker))
            {
                File.WriteAllText(marker, "closed");
            }
        }

        public void SaveModifications()
        {
            ApiAccessGuard.RequireCompileTimeCallSite("SaveModifications");
            var marker = Environment.GetEnvironmentVariable("FAKE_VMS_SAVE_MARKER");
            if (!string.IsNullOrWhiteSpace(marker))
            {
                File.AppendAllText(marker, "saved\n");
            }
        }

        public void Dispose()
        {
            var marker = Environment.GetEnvironmentVariable("FAKE_VMS_DISPOSE_MARKER");
            if (!string.IsNullOrWhiteSpace(marker))
            {
                File.WriteAllText(marker, "disposed");
            }
        }
    }

    public sealed class PatientSummary
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public sealed class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class Patient
    {
        public Patient()
        {
            Courses = new List<Course>();
            StructureSets = new List<StructureSet>();
        }

        public string Id { get; set; }
        public IList<Course> Courses { get; private set; }
        public IList<StructureSet> StructureSets { get; private set; }
    }

    public sealed class Course
    {
        public Course()
        {
            PlanSetups = new List<PlanSetup>();
            PlanSums = new List<PlanSum>();
        }

        public string Id { get; set; }
        public Patient Patient { get; set; }
        public IList<PlanSetup> PlanSetups { get; private set; }
        public IList<PlanSum> PlanSums { get; private set; }

        public ExternalPlanSetup AddExternalPlanSetup(StructureSet structureSet)
        {
            ApiAccessGuard.RequireCompileTimeCallSite("AddExternalPlanSetup");
            var plan = new ExternalPlanSetup
            {
                Id = "NEW-PLAN",
                Course = this,
                StructureSet = structureSet
            };
            PlanSetups.Add(plan);
            return plan;
        }
    }

    public class PlanSetup
    {
        public string Id { get; set; }
        public Course Course { get; set; }
        public StructureSet StructureSet { get; set; }
    }

    public sealed class ExternalPlanSetup : PlanSetup
    {
    }

    public sealed class PlanSum
    {
        public string Id { get; set; }
        public Course Course { get; set; }
        public StructureSet StructureSet { get; set; }
    }

    public sealed class StructureSet
    {
        public string Id { get; set; }
        public Image Image { get; set; }
    }

    public sealed class Image
    {
        public string Id { get; set; }
    }

#pragma warning disable 0649
    public sealed class ScriptContext
    {
        private object m_context;
        private object m_currentUser;
        private User m_user;
        private Course m_course;
        private Image m_image;
        private Patient m_patient;
        private PlanSetup m_planSetup;
        private List<PlanSetup> m_planSetups;
        private List<PlanSum> m_planSums;
        private PlanSum m_planSum;
        private StructureSet m_structureSet;
        private string m_appName;

        public ScriptContext(object context, object user, object dvhEstimation, string appName)
        {
            m_context = context;
            m_currentUser = user;
            m_appName = appName;
            m_planSetups = new List<PlanSetup>();
            m_planSums = new List<PlanSum>();
        }

        public User CurrentUser { get { return m_user; } }
        public Course Course { get { return m_course; } }
        public Image Image { get { return m_image; } }
        public Patient Patient { get { return m_patient; } }
        public PlanSetup PlanSetup { get { return m_planSetup; } }
        public IEnumerable<PlanSetup> PlansInScope { get { return m_planSetups; } }
        public IEnumerable<PlanSum> PlanSumsInScope { get { return m_planSums; } }
        public PlanSum PlanSum { get { return m_planSum; } }
        public StructureSet StructureSet { get { return m_structureSet; } }
        public string ApplicationName { get { return m_appName; } }
    }
#pragma warning restore 0649
}
