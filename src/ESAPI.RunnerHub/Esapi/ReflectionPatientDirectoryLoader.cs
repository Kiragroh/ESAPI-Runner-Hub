using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EsapiRunnerHub.Patients;

namespace EsapiRunnerHub.Esapi
{
    public sealed class ReflectionPatientDirectoryLoader
    {
        private const string ApplicationTypeName = "VMS.TPS.Common.Model.API.Application";

        public PatientDirectoryLoadResult Load(string apiAssemblyPath, string typesAssemblyPath)
        {
            if (string.IsNullOrWhiteSpace(apiAssemblyPath) || !File.Exists(apiAssemblyPath))
            {
                return PatientDirectoryLoadResult.Offline("assembly_missing", "ESAPI API assembly was not found.");
            }

            if (!string.IsNullOrWhiteSpace(typesAssemblyPath) && !File.Exists(typesAssemblyPath))
            {
                return PatientDirectoryLoadResult.Offline("assembly_missing", "ESAPI types assembly was not found.");
            }

            var assemblyPaths = new[] { apiAssemblyPath, typesAssemblyPath }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToList();
            ResolveEventHandler resolver = (sender, arguments) => ResolveKnownAssembly(arguments.Name, assemblyPaths);
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            object application = null;
            try
            {
                foreach (var dependencyPath in assemblyPaths.Skip(1))
                {
                    Assembly.LoadFrom(dependencyPath);
                }

                var apiAssembly = Assembly.LoadFrom(assemblyPaths[0]);
                var applicationType = apiAssembly.GetType(ApplicationTypeName, true, false);
                application = CreateApplication(applicationType);
                var summariesProperty = applicationType.GetProperty("PatientSummaries", BindingFlags.Public | BindingFlags.Instance);
                if (summariesProperty == null)
                {
                    return PatientDirectoryLoadResult.Offline("api_incompatible", "ESAPI PatientSummaries is not available.");
                }

                var summaries = summariesProperty.GetValue(application, null) as IEnumerable;
                var patients = new List<PatientRecord>();
                if (summaries != null)
                {
                    var sourceIndex = 0;
                    foreach (var summary in summaries)
                    {
                        if (summary == null)
                        {
                            continue;
                        }

                        var summaryType = summary.GetType();
                        var id = ReadString(summaryType, summary, "Id");
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        patients.Add(new PatientRecord(
                            id,
                            ReadString(summaryType, summary, "FirstName"),
                            ReadString(summaryType, summary, "LastName"),
                            sourceIndex++));
                    }
                }

                return PatientDirectoryLoadResult.Available(patients);
            }
            catch (Exception exception)
            {
                var root = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                return PatientDirectoryLoadResult.Offline("esapi_unavailable", root.Message);
            }
            finally
            {
                DisposeApplication(application);
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }

        private static object CreateApplication(Type applicationType)
        {
            var factory = applicationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "CreateApplication")
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
            if (factory == null)
            {
                throw new MissingMethodException(ApplicationTypeName, "CreateApplication");
            }

            var arguments = factory.GetParameters()
                .Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null)
                .ToArray();
            return factory.Invoke(null, arguments);
        }

        private static string ReadString(Type type, object instance, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            var value = property == null ? null : property.GetValue(instance, null);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static Assembly ResolveKnownAssembly(string requestedName, IEnumerable<string> assemblyPaths)
        {
            var requested = new AssemblyName(requestedName).Name;
            foreach (var path in assemblyPaths)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(path), requested, StringComparison.OrdinalIgnoreCase))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }

        private static void DisposeApplication(object application)
        {
            if (application == null)
            {
                return;
            }

            var disposable = application as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
                return;
            }

            var dispose = application.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
            if (dispose != null)
            {
                dispose.Invoke(application, null);
            }
        }
    }
}
