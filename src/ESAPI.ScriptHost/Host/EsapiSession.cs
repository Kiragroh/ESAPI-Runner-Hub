using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EsapiScriptHost.Contracts;

namespace EsapiScriptHost.Host
{
    public sealed class EsapiSession : IDisposable
    {
        private readonly ResolveEventHandler resolver;
        private object application;
        private object patient;
        private Assembly apiAssembly;
        private bool saved;
        private bool disposed;

        private EsapiSession(object application, object patient, Assembly apiAssembly, ResolveEventHandler resolver)
        {
            this.application = application;
            this.patient = patient;
            this.apiAssembly = apiAssembly;
            this.resolver = resolver;
        }

        public object Application { get { return application; } }
        public object Patient { get { return patient; } }
        public object CurrentUser { get { return ContextResolver.ReadObject(application, "CurrentUser"); } }
        public Assembly ApiAssembly { get { return apiAssembly; } }

        public static EsapiSession Open(ContextLaunchPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var paths = new[] { payload.ApiAssemblyPath, payload.TypesAssemblyPath }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToList();
            if (paths.Count == 0 || !File.Exists(paths[0])) throw new FileNotFoundException("ESAPI API assembly was not found.");
            foreach (var path in paths) if (!File.Exists(path)) throw new FileNotFoundException("An ESAPI assembly was not found.");

            ResolveEventHandler resolver = (sender, arguments) => ResolveKnownAssembly(arguments.Name, paths);
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            object application = null;
            object patient = null;
            try
            {
                foreach (var dependency in paths.Skip(1)) Assembly.LoadFrom(dependency);
                var api = Assembly.LoadFrom(paths[0]);
                var applicationType = api.GetType("VMS.TPS.Common.Model.API.Application", true, false);
                var factory = applicationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.Name == "CreateApplication")
                    .OrderBy(method => method.GetParameters().Length)
                    .FirstOrDefault();
                if (factory == null) throw new MissingMethodException(applicationType.FullName, "CreateApplication");
                application = factory.Invoke(null, factory.GetParameters().Select(parameter => parameter.HasDefaultValue ? parameter.DefaultValue : null).ToArray());
                if (!string.IsNullOrWhiteSpace(payload.PatientId))
                {
                    patient = Invoke(application, "OpenPatientById", payload.PatientId);
                    if (patient == null) throw new InvalidOperationException("The selected patient could not be opened.");
                }
                return new EsapiSession(application, patient, api, resolver);
            }
            catch
            {
                if (patient != null && application != null) InvokeIfPresent(application, "ClosePatient");
                DisposeObject(application);
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
                throw;
            }
        }

        public void SaveModifications()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EsapiSession));
            if (saved) throw new InvalidOperationException("Modifications have already been saved.");
            Invoke(application, "SaveModifications");
            saved = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (patient != null) InvokeIfPresent(application, "ClosePatient");
            }
            finally
            {
                patient = null;
                DisposeObject(application);
                application = null;
                apiAssembly = null;
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
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
            var method = instance == null ? null : instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (method != null) method.Invoke(instance, null);
        }

        private static Assembly ResolveKnownAssembly(string requestedName, IEnumerable<string> paths)
        {
            var requested = new AssemblyName(requestedName).Name;
            var path = paths.FirstOrDefault(candidate => string.Equals(Path.GetFileNameWithoutExtension(candidate), requested, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : Assembly.LoadFrom(path);
        }

        private static void DisposeObject(object instance)
        {
            var disposable = instance as IDisposable;
            if (disposable != null) disposable.Dispose();
            else InvokeIfPresent(instance, "Dispose");
        }
    }
}
