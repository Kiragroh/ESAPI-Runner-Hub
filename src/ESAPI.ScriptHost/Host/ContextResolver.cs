using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EsapiRunnerHub.Launching;

namespace EsapiScriptHost.Host
{
    public sealed class ResolvedContext
    {
        public ResolvedContext()
        {
            PlansInScope = new List<object>();
            PlanSumsInScope = new List<object>();
        }

        public object CurrentUser { get; set; }
        public object Patient { get; set; }
        public object Course { get; set; }
        public object Plan { get; set; }
        public object PlanSum { get; set; }
        public object StructureSet { get; set; }
        public object Image { get; set; }
        public Assembly ApiAssembly { get; set; }
        public IList<object> PlansInScope { get; private set; }
        public IList<object> PlanSumsInScope { get; private set; }
    }

    public sealed class ContextResolver
    {
        public ResolvedContext Resolve(object patient, object currentUser, ContextLaunchPayload payload)
        {
            return Resolve(patient, currentUser, payload, patient == null ? null : patient.GetType().Assembly);
        }

        public ResolvedContext Resolve(object patient, object currentUser, ContextLaunchPayload payload, Assembly apiAssembly)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var resolved = new ResolvedContext { CurrentUser = currentUser, Patient = patient, ApiAssembly = apiAssembly };
            if (patient == null)
            {
                if (HasPlanningContext(payload))
                    throw new InvalidOperationException("A patient is required for the selected planning context.");
                return resolved;
            }

            var courses = ReadEnumerable(patient, "Courses").ToList();
            var allPlans = courses.SelectMany(course => ReadEnumerable(course, "PlanSetups")).ToList();
            var allPlanSums = courses.SelectMany(course => ReadEnumerable(course, "PlanSums")).ToList();
            var structureSets = ReadEnumerable(patient, "StructureSets").ToList();
            var images = structureSets.Select(item => ReadObject(item, "Image"))
                .Where(item => item != null)
                .GroupBy(item => ReadString(item, "Id"), StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (!string.IsNullOrWhiteSpace(payload.CourseId))
                resolved.Course = FindUnique(courses, payload.CourseId, "course");
            if (!string.IsNullOrWhiteSpace(payload.PlanId))
                resolved.Plan = FindUnique(allPlans, payload.PlanId, "plan");
            if (!string.IsNullOrWhiteSpace(payload.PlanSumId))
                resolved.PlanSum = FindUnique(allPlanSums, payload.PlanSumId, "plan sum");
            if (!string.IsNullOrWhiteSpace(payload.StructureSetId))
                resolved.StructureSet = FindUnique(structureSets, payload.StructureSetId, "structure set");

            foreach (var id in payload.PlanIdsInScope ?? new List<string>())
                resolved.PlansInScope.Add(FindUnique(allPlans, id, "plan in scope"));
            foreach (var id in payload.PlanSumIdsInScope ?? new List<string>())
                resolved.PlanSumsInScope.Add(FindUnique(allPlanSums, id, "plan sum in scope"));

            if (resolved.Plan != null)
            {
                ValidateRelatedId(resolved.Plan, "Course", payload.CourseId, "course");
                ValidateRelatedId(resolved.Plan, "StructureSet", payload.StructureSetId, "structure set");
                if (resolved.Course == null) resolved.Course = ReadObject(resolved.Plan, "Course");
                if (resolved.StructureSet == null) resolved.StructureSet = ReadObject(resolved.Plan, "StructureSet");
            }
            if (resolved.PlanSum != null)
            {
                ValidateRelatedId(resolved.PlanSum, "Course", payload.CourseId, "course");
                ValidateRelatedId(resolved.PlanSum, "StructureSet", payload.StructureSetId, "structure set");
                if (resolved.Course == null) resolved.Course = ReadObject(resolved.PlanSum, "Course");
                if (resolved.StructureSet == null) resolved.StructureSet = ReadObject(resolved.PlanSum, "StructureSet");
            }

            resolved.Image = ReadObject(resolved.StructureSet, "Image");
            if (!string.IsNullOrWhiteSpace(payload.ImageId))
            {
                var requestedImage = FindUnique(images, payload.ImageId, "image");
                if (resolved.Image != null && !string.Equals(ReadString(resolved.Image, "Id"), payload.ImageId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The selected image does not match the structure set.");
                if (resolved.Image == null) resolved.Image = requestedImage;
            }
            return resolved;
        }

        private static bool HasPlanningContext(ContextLaunchPayload payload)
        {
            return !string.IsNullOrWhiteSpace(payload.CourseId) || !string.IsNullOrWhiteSpace(payload.PlanId) ||
                   !string.IsNullOrWhiteSpace(payload.PlanSumId) || !string.IsNullOrWhiteSpace(payload.StructureSetId) ||
                   !string.IsNullOrWhiteSpace(payload.ImageId) || (payload.PlanIdsInScope != null && payload.PlanIdsInScope.Count > 0) ||
                   (payload.PlanSumIdsInScope != null && payload.PlanSumIdsInScope.Count > 0);
        }

        private static object FindUnique(IEnumerable<object> values, string id, string kind)
        {
            var matches = values.Where(item => string.Equals(ReadString(item, "Id"), id, StringComparison.Ordinal)).Take(2).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("The selected " + kind + " could not be resolved uniquely.");
            return matches[0];
        }

        private static void ValidateRelatedId(object instance, string propertyName, string expectedId, string kind)
        {
            if (string.IsNullOrWhiteSpace(expectedId)) return;
            if (!string.Equals(ReadString(ReadObject(instance, propertyName), "Id"), expectedId, StringComparison.Ordinal))
                throw new InvalidOperationException("The selected plan " + kind + " does not match the requested context.");
        }

        internal static IEnumerable<object> ReadEnumerable(object instance, string propertyName)
        {
            var enumerable = ReadObject(instance, propertyName) as IEnumerable;
            return enumerable == null ? Enumerable.Empty<object>() : enumerable.Cast<object>().Where(item => item != null);
        }

        internal static object ReadObject(object instance, string propertyName)
        {
            if (instance == null) return null;
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(instance, null);
        }

        internal static string ReadString(object instance, string propertyName)
        {
            var value = ReadObject(instance, propertyName);
            return value == null ? string.Empty : Convert.ToString(value);
        }
    }
}
