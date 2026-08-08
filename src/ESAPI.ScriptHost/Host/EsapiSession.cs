using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EsapiScriptHost.Contracts;
using VMS.TPS.Common.Model.API;

namespace EsapiScriptHost.Host
{
    public sealed class EsapiSession : IDisposable
    {
        private readonly ResolveEventHandler resolver;
        private Application application;
        private Patient patient;
        private Assembly apiAssembly;
        private bool saved;
        private bool disposed;

        private EsapiSession(Application application, Patient patient, Assembly apiAssembly, ResolveEventHandler resolver)
        {
            this.application = application;
            this.patient = patient;
            this.apiAssembly = apiAssembly;
            this.resolver = resolver;
        }

        public object Application { get { return application; } }
        public object Patient { get { return patient; } }
        public object CurrentUser { get { return application == null ? null : application.CurrentUser; } }
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
            Application application = null;
            Patient patient = null;
            try
            {
                foreach (var dependency in paths.Skip(1)) Assembly.LoadFrom(dependency);
                var api = Assembly.LoadFrom(paths[0]);
                if (!string.Equals(typeof(VMS.TPS.Common.Model.API.Application).Assembly.FullName, api.FullName, StringComparison.Ordinal))
                    throw new InvalidOperationException("The loaded Eclipse API assembly does not match the host compile-time reference.");
                application = VMS.TPS.Common.Model.API.Application.CreateApplication();
                if (!string.IsNullOrWhiteSpace(payload.PatientId))
                {
                    patient = application.OpenPatientById(payload.PatientId);
                    if (patient == null) throw new InvalidOperationException("The selected patient could not be opened.");
                }
                return new EsapiSession(application, patient, api, resolver);
            }
            catch
            {
                if (patient != null && application != null) application.ClosePatient();
                if (application != null) application.Dispose();
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
                throw;
            }
        }

        public void SaveModifications()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EsapiSession));
            if (saved) throw new InvalidOperationException("Modifications have already been saved.");
            application.SaveModifications();
            saved = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (patient != null) application.ClosePatient();
            }
            finally
            {
                patient = null;
                if (application != null) application.Dispose();
                application = null;
                apiAssembly = null;
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }

        private static Assembly ResolveKnownAssembly(string requestedName, IEnumerable<string> paths)
        {
            var requested = new AssemblyName(requestedName).Name;
            var path = paths.FirstOrDefault(candidate => string.Equals(Path.GetFileNameWithoutExtension(candidate), requested, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : Assembly.LoadFrom(path);
        }

    }
}
