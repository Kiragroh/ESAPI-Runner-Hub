using System;
using System.Collections.Generic;
using System.Linq;
using EsapiRunnerHub.Configuration;

namespace EsapiRunnerHub.Context
{
    public sealed class ContextSelection
    {
        public ContextSelection()
        {
            PlanIdsInScope = new List<string>();
            PlanSumIdsInScope = new List<string>();
        }

        public string PatientId { get; set; }
        public string CourseId { get; set; }
        public string PlanId { get; set; }
        public string PlanSumId { get; set; }
        public string StructureSetId { get; set; }
        public string ImageId { get; set; }
        public IList<string> PlanIdsInScope { get; private set; }
        public IList<string> PlanSumIdsInScope { get; private set; }

        public void ClearPlanningContext()
        {
            CourseId = null;
            PlanId = null;
            PlanSumId = null;
            StructureSetId = null;
            ImageId = null;
            PlanIdsInScope.Clear();
            PlanSumIdsInScope.Clear();
        }

        public void SelectPlan(ContextDirectory directory, string planId)
        {
            var plan = FindSingle(directory == null ? null : directory.Plans, item => item.Id, planId, "plan");
            CourseId = plan.CourseId;
            PlanId = plan.Id;
            PlanSumId = null;
            StructureSetId = plan.StructureSetId;
            ImageId = plan.ImageId;
            PlanIdsInScope.Clear();
            PlanIdsInScope.Add(plan.Id);
            PlanSumIdsInScope.Clear();
        }

        public void SelectPlanSum(ContextDirectory directory, string planSumId)
        {
            var planSum = FindSingle(directory == null ? null : directory.PlanSums, item => item.Id, planSumId, "plan sum");
            CourseId = planSum.CourseId;
            PlanId = null;
            PlanSumId = planSum.Id;
            StructureSetId = planSum.StructureSetId;
            ImageId = planSum.ImageId;
            PlanIdsInScope.Clear();
            PlanSumIdsInScope.Clear();
            PlanSumIdsInScope.Add(planSum.Id);
        }

        public void SelectStructureSet(ContextDirectory directory, string structureSetId)
        {
            var structureSet = FindSingle(directory == null ? null : directory.StructureSets, item => item.Id, structureSetId, "structure set");
            CourseId = null;
            PlanId = null;
            PlanSumId = null;
            StructureSetId = structureSet.Id;
            ImageId = structureSet.ImageId;
            PlanIdsInScope.Clear();
            PlanSumIdsInScope.Clear();
        }

        public void SelectImage(ContextDirectory directory, string imageId)
        {
            var image = FindSingle(directory == null ? null : directory.Images, item => item.Id, imageId, "image");
            if (!string.IsNullOrWhiteSpace(StructureSetId))
            {
                var structureSet = (directory == null ? Enumerable.Empty<StructureSetDescriptor>() : directory.StructureSets)
                    .FirstOrDefault(item => string.Equals(item.Id, StructureSetId, StringComparison.Ordinal));
                if (structureSet == null || !string.Equals(structureSet.ImageId, image.Id, StringComparison.Ordinal))
                {
                    CourseId = null;
                    PlanId = null;
                    PlanSumId = null;
                    StructureSetId = null;
                    PlanIdsInScope.Clear();
                    PlanSumIdsInScope.Clear();
                }
            }
            ImageId = image.Id;
        }

        public string MissingFor(ContextRequirement requirement)
        {
            if (requirement == ContextRequirement.None) return string.Empty;
            if (string.IsNullOrWhiteSpace(PatientId)) return "Patient required";
            if (requirement == ContextRequirement.Patient) return string.Empty;
            if (requirement == ContextRequirement.Course)
                return string.IsNullOrWhiteSpace(CourseId) ? "Course required" : string.Empty;
            if (requirement == ContextRequirement.Image)
                return string.IsNullOrWhiteSpace(ImageId) ? "Image required" : string.Empty;
            if (requirement == ContextRequirement.StructureSet)
                return string.IsNullOrWhiteSpace(StructureSetId) ? "Structure set required" : string.Empty;
            if (requirement == ContextRequirement.Plan)
                return string.IsNullOrWhiteSpace(PlanId) ? "Plan required" : string.Empty;
            if (requirement == ContextRequirement.PlanningItem)
                return string.IsNullOrWhiteSpace(PlanId) && string.IsNullOrWhiteSpace(PlanSumId)
                    ? "Plan or plan sum required" : string.Empty;
            return string.IsNullOrWhiteSpace(PlanId) && string.IsNullOrWhiteSpace(PlanSumId) &&
                   string.IsNullOrWhiteSpace(StructureSetId)
                ? "Plan, plan sum, or structure set required" : string.Empty;
        }

        private static T FindSingle<T>(IEnumerable<T> values, Func<T, string> id, string expectedId, string kind)
        {
            var matches = (values ?? Enumerable.Empty<T>())
                .Where(item => string.Equals(id(item), expectedId, StringComparison.Ordinal))
                .Take(2)
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException("The selected " + kind + " could not be resolved uniquely.");
            }

            return matches[0];
        }
    }
}
