using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Game.Chess.Tests.Integration.Runner
{
    public sealed class TestLoadContext : AssemblyLoadContext
    {
        private readonly string _basePath;

        public TestLoadContext(string basePath)
            : base(isCollectible: true)
        {
            _basePath = basePath;
            Resolving += OnResolving;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string asmPath = Path.Combine(_basePath, assemblyName.Name + ".dll");
            if (File.Exists(asmPath))
            {
                return LoadFromAssemblyPath(asmPath);
            }
            return null;
        }

        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            try
            {
                string asmPath = Path.Combine(_basePath, assemblyName.Name + ".dll");
                if (File.Exists(asmPath))
                {
                    return LoadFromAssemblyPath(asmPath);
                }
            }
            catch { }
            return null;
        }
    }
}
