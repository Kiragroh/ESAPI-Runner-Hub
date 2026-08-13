using System;
using System.Collections.Generic;

namespace EsapiRunnerHub.Context
{
    public sealed class CourseDescriptor
    {
        public string Id { get; set; }
        public string Display { get { return Id ?? string.Empty; } }
    }

    public sealed class PlanDescriptor
    {
        public string Id { get; set; }
        public string CourseId { get; set; }
        public string StructureSetId { get; set; }
        public string ImageId { get; set; }
        public string Kind { get; set; }
        public string Display { get { return (CourseId ?? string.Empty) + " · " + (Id ?? string.Empty) + " · " + (Kind ?? "Plan"); } }
    }

    public sealed class PlanSumDescriptor
    {
        public string Id { get; set; }
        public string CourseId { get; set; }
        public string StructureSetId { get; set; }
        public string ImageId { get; set; }
        public string Display { get { return (CourseId ?? string.Empty) + " · " + (Id ?? string.Empty) + " · PlanSum"; } }
    }

    public sealed class StructureSetDescriptor
    {
        public string Id { get; set; }
        public string ImageId { get; set; }
        public string Display { get { return string.IsNullOrWhiteSpace(ImageId) ? Id : Id + " · " + ImageId; } }
    }

    public sealed class ImageDescriptor
    {
        public string Id { get; set; }
        public string Display { get { return Id ?? string.Empty; } }
    }

    public sealed class ContextDirectory
    {
        public ContextDirectory()
        {
            Courses = new List<CourseDescriptor>();
            Plans = new List<PlanDescriptor>();
            PlanSums = new List<PlanSumDescriptor>();
            StructureSets = new List<StructureSetDescriptor>();
            Images = new List<ImageDescriptor>();
        }

        public IList<CourseDescriptor> Courses { get; private set; }
        public IList<PlanDescriptor> Plans { get; private set; }
        public IList<PlanSumDescriptor> PlanSums { get; private set; }
        public IList<StructureSetDescriptor> StructureSets { get; private set; }
        public IList<ImageDescriptor> Images { get; private set; }
    }

    public sealed class ContextDirectoryLoadResult
    {
        private ContextDirectoryLoadResult(bool available, ContextDirectory directory, string errorCode, Exception exception)
        {
            IsAvailable = available;
            Directory = directory ?? new ContextDirectory();
            ErrorCode = errorCode ?? string.Empty;
            Exception = exception;
        }

        public bool IsAvailable { get; private set; }
        public ContextDirectory Directory { get; private set; }
        public IList<CourseDescriptor> Courses { get { return Directory.Courses; } }
        public IList<PlanDescriptor> Plans { get { return Directory.Plans; } }
        public IList<PlanSumDescriptor> PlanSums { get { return Directory.PlanSums; } }
        public IList<StructureSetDescriptor> StructureSets { get { return Directory.StructureSets; } }
        public IList<ImageDescriptor> Images { get { return Directory.Images; } }
        public string ErrorCode { get; private set; }
        public Exception Exception { get; private set; }

        public static ContextDirectoryLoadResult Available(ContextDirectory directory)
        {
            return new ContextDirectoryLoadResult(true, directory, string.Empty, null);
        }

        public static ContextDirectoryLoadResult Offline(string errorCode, Exception exception)
        {
            return new ContextDirectoryLoadResult(false, new ContextDirectory(), errorCode, exception);
        }
    }
}
