using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EsapiRunnerHub.Context;

namespace EsapiRunnerHub.Esapi
{
    public sealed class ReflectionContextDirectoryLoader
    {
        private const string ApplicationTypeName = "VMS.TPS.Common.Model.API.Application";

        public ContextDirectoryLoadResult Load(string apiAssemblyPath, string typesAssemblyPath, string patientId)
        {
            object application = null;
            object patient = null;
            ResolveEventHandler resolver = null;
            try
            {
                var location = new EsapiAssemblyLocator().Locate(apiAssemblyPath, typesAssemblyPath);
                if (string.IsNullOrWhiteSpace(location.ApiAssemblyPath) || !File.Exists(location.ApiAssemblyPath))
                    return ContextDirectoryLoadResult.Offline("assembly_missing", null);

                var assemblyPaths = new[] { location.ApiAssemblyPath, location.TypesAssemblyPath }
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Select(Path.GetFullPath)
                    .ToList();
                resolver = (sender, arguments) => ResolveKnownAssembly(arguments.Name, assemblyPaths);
                AppDomain.CurrentDomain.AssemblyResolve += resolver;
                foreach (var dependency in assemblyPaths.Skip(1)) Assembly.LoadFrom(dependency);

                var apiAssembly = Assembly.LoadFrom(assemblyPaths[0]);
                var applicationType = apiAssembly.GetType(ApplicationTypeName, true, false);
                application = CreateApplication(applicationType);
                patient = Invoke(application, "OpenPatientById", patientId);
                if (patient == null) return ContextDirectoryLoadResult.Offline("patient_not_found", null);

                return ContextDirectoryLoadResult.Available(CopyDirectory(patient));
            }
            catch (Exception exception)
            {
                var root = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                return ContextDirectoryLoadResult.Offline("context_unavailable", root);
            }
            finally
            {
                if (patient != null && application != null) InvokeIfPresent(application, "ClosePatient");
                DisposeApplication(application);
                if (resolver != null) AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }

        private static ContextDirectory CopyDirectory(object patient)
        {
            var directory = new ContextDirectory();
            foreach (var structureSet in ReadEnumerable(patient, "StructureSets"))
            {
                directory.StructureSets.Add(new StructureSetDescriptor
                {
                    Id = ReadString(structureSet, "Id"),
                    ImageId = ReadNestedString(structureSet, "Image", "Id")
                });
            }

            foreach (var course in ReadEnumerable(patient, "Courses"))
            {
                var courseId = ReadString(course, "Id");
                directory.Courses.Add(new CourseDescriptor { Id = courseId });
                foreach (var plan in ReadEnumerable(course, "PlanSetups"))
                {
                    var structureSet = ReadObject(plan, "StructureSet");
                    directory.Plans.Add(new PlanDescriptor
                    {
                        Id = ReadString(plan, "Id"),
                        CourseId = courseId,
                        StructureSetId = ReadString(structureSet, "Id"),
                        ImageId = ReadNestedString(structureSet, "Image", "Id"),
                        Kind = plan.GetType().Name
                    });
                }

                foreach (var planSum in ReadEnumerable(course, "PlanSums"))
                {
                    var structureSet = ReadObject(planSum, "StructureSet");
                    directory.PlanSums.Add(new PlanSumDescriptor
                    {
                        Id = ReadString(planSum, "Id"),
                        CourseId = courseId,
                        StructureSetId = ReadString(structureSet, "Id"),
                        ImageId = ReadNestedString(structureSet, "Image", "Id")
                    });
                }
            }

            return directory;
        }

        private static object CreateApplication(Type applicationType)
        {
            var factory = applicationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "CreateApplication")
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
            if (factory == null) throw new MissingMethodException(ApplicationTypeName, "CreateApplication");
            return factory.Invoke(null, factory.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray());
        }

        private static object Invoke(object instance, string name, params object[] arguments)
        {
            var method = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
            if (method == null) throw new MissingMethodException(instance.GetType().FullName, name);
            return method.Invoke(instance, arguments);
        }

        private static void InvokeIfPresent(object instance, string name)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (method != null) method.Invoke(instance, null);
        }

        private static IEnumerable<object> ReadEnumerable(object instance, string propertyName)
        {
            var value = ReadObject(instance, propertyName) as IEnumerable;
            return value == null ? Enumerable.Empty<object>() : value.Cast<object>().Where(item => item != null);
        }

        private static object ReadObject(object instance, string propertyName)
        {
            if (instance == null) return null;
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static string ReadString(object instance, string propertyName)
        {
            var value = ReadObject(instance, propertyName);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static string ReadNestedString(object instance, string propertyName, string nestedPropertyName)
        {
            return ReadString(ReadObject(instance, propertyName), nestedPropertyName);
        }

        private static Assembly ResolveKnownAssembly(string requestedName, IEnumerable<string> paths)
        {
            var requested = new AssemblyName(requestedName).Name;
            var path = paths.FirstOrDefault(candidate => string.Equals(Path.GetFileNameWithoutExtension(candidate), requested, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : Assembly.LoadFrom(path);
        }

        private static void DisposeApplication(object application)
        {
            if (application == null) return;
            var disposable = application as IDisposable;
            if (disposable != null) disposable.Dispose();
            else InvokeIfPresent(application, "Dispose");
        }
    }
}
