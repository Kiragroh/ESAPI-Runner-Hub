using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EsapiScriptHost.Contracts;

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
                    throw new ContextResolutionException("patient_missing", "A patient is required for the selected planning context.");
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
                resolved.Plan = FindUnique(resolved.Course == null ? allPlans : ReadEnumerable(resolved.Course, "PlanSetups"), payload.PlanId, "plan");
            if (!string.IsNullOrWhiteSpace(payload.PlanSumId))
                resolved.PlanSum = FindUnique(resolved.Course == null ? allPlanSums : ReadEnumerable(resolved.Course, "PlanSums"), payload.PlanSumId, "plan sum");

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

            if (!string.IsNullOrWhiteSpace(payload.StructureSetId))
            {
                if (resolved.StructureSet == null)
                    resolved.StructureSet = FindUnique(structureSets, payload.StructureSetId, "structure set");
                else if (!string.Equals(ReadString(resolved.StructureSet, "Id"), payload.StructureSetId, StringComparison.Ordinal))
                    throw new ContextResolutionException("planning_relation_mismatch", "The selected planning item structure set does not match the requested context.");
            }

            AddScope(resolved.PlansInScope, allPlans, payload.PlanIdsInScope, resolved.Plan, "plan in scope");
            AddScope(resolved.PlanSumsInScope, allPlanSums, payload.PlanSumIdsInScope, resolved.PlanSum, "plan sum in scope");

            resolved.Image = ReadObject(resolved.StructureSet, "Image");
            if (!string.IsNullOrWhiteSpace(payload.ImageId))
            {
                var requestedImage = FindUnique(images, payload.ImageId, "image");
                if (resolved.Image != null && !string.Equals(ReadString(resolved.Image, "Id"), payload.ImageId, StringComparison.Ordinal))
                    throw new ContextResolutionException("image_relation_mismatch", "The selected image does not match the structure set.");
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
            if (matches.Count != 1)
                throw new ContextResolutionException(ReasonFor(kind, matches.Count),
                    "The selected " + kind + " could not be resolved uniquely.");
            return matches[0];
        }

        private static void AddScope(IList<object> target, IList<object> available, IList<string> requestedIds,
            object selected, string kind)
        {
            var ids = requestedIds ?? new List<string>();
            if (ids.Count == 1 && selected != null &&
                string.Equals(ReadString(selected, "Id"), ids[0], StringComparison.Ordinal))
            {
                target.Add(selected);
                return;
            }

            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                int occurrence;
                occurrences.TryGetValue(id, out occurrence);
                var matches = available.Where(item => string.Equals(ReadString(item, "Id"), id, StringComparison.Ordinal)).ToList();
                if (occurrence >= matches.Count)
                    throw new ContextResolutionException(ReasonFor(kind, matches.Count),
                        "The selected " + kind + " could not be resolved uniquely.");
                target.Add(matches[occurrence]);
                occurrences[id] = occurrence + 1;
            }
        }

        private static void ValidateRelatedId(object instance, string propertyName, string expectedId, string kind)
        {
            if (string.IsNullOrWhiteSpace(expectedId)) return;
            if (!string.Equals(ReadString(ReadObject(instance, propertyName), "Id"), expectedId, StringComparison.Ordinal))
                throw new ContextResolutionException("planning_relation_mismatch",
                    "The selected plan " + kind + " does not match the requested context.");
        }

        private static string ReasonFor(string kind, int matchCount)
        {
            return (kind ?? "context").Replace(' ', '_') + (matchCount == 0 ? "_missing" : "_ambiguous");
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
