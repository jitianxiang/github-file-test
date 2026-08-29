// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

#if !NETFRAMEWORK
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using CommonServiceLocator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ServiceLocation.Tests
{
    [TestClass]
    public class ActivationExceptionAssemblyContractFixture
    {
        private static readonly string[] DesktopTargetFrameworks =
        {
            "net46",
            "net47",
            "net48"
        };

        private static readonly string[] ModernTargetFrameworks =
        {
            "netcoreapp3.0",
            "netcoreapp2.0",
            "netcoreapp1.0",
            "netstandard1.0",
            "netstandard2.0",
            "netstandard2.1",
            "net50",
            "net6.0",
            "net7.0"
        };

        [TestMethod]
        public void DesktopAssembliesExposeLegacySerializationContract()
        {
            foreach (string targetFramework in DesktopTargetFrameworks)
            {
                InspectAssembly(targetFramework, type =>
                {
#pragma warning disable SYSLIB0050 // Type.IsSerializable is the metadata under test.
                    Assert.IsTrue(type.IsSerializable, targetFramework);
#pragma warning restore SYSLIB0050

                    ConstructorInfo[] constructors = GetSerializationConstructors(type);
                    Assert.AreEqual(1, constructors.Length, targetFramework);
                    Assert.IsTrue(constructors[0].IsFamily, targetFramework);
                    Assert.IsFalse(constructors[0].IsFamilyOrAssembly, targetFramework);
                    Assert.IsFalse(constructors[0].IsStatic, targetFramework);
                });
            }
        }

        [TestMethod]
        public void ModernAssembliesDoNotExposeLegacySerializationContract()
        {
            foreach (string targetFramework in ModernTargetFrameworks)
            {
                InspectAssembly(targetFramework, type =>
                {
#pragma warning disable SYSLIB0050 // Type.IsSerializable is the metadata under test.
                    Assert.IsFalse(type.IsSerializable, targetFramework);
#pragma warning restore SYSLIB0050
                    Assert.AreEqual(0, GetSerializationConstructors(type).Length, targetFramework);
                });
            }
        }

        [TestMethod]
        public void AllAssembliesPreservePublicApiAndStrongNameIdentity()
        {
            foreach (string targetFramework in DesktopTargetFrameworks.Concat(ModernTargetFrameworks))
            {
                InspectAssembly(targetFramework, type =>
                {
                    Assert.AreEqual(typeof(Exception).FullName, type.BaseType.FullName, targetFramework);

                    string[] constructorSignatures = type
                        .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                        .Select(GetConstructorSignature)
                        .OrderBy(signature => signature, StringComparer.Ordinal)
                        .ToArray();

                    CollectionAssert.AreEqual(
                        new[]
                        {
                            "()",
                            "(System.String)",
                            "(System.String,System.Exception)"
                        },
                        constructorSignatures,
                        targetFramework);

                    AssemblyName identity = type.Assembly.GetName();
                    Assert.AreEqual("CommonServiceLocator", identity.Name, targetFramework);
                    Assert.AreEqual(new Version(2, 0, 7, 0), identity.Version, targetFramework);
                    Assert.AreEqual(string.Empty, identity.CultureName ?? string.Empty, targetFramework);
                    Assert.AreEqual(
                        "489b6accfaf20ef0",
                        string.Concat(identity.GetPublicKeyToken().Select(value => value.ToString("x2"))),
                        targetFramework);
                });
            }
        }

        [TestMethod]
        public void ExistingConstructorsRemainUsableOnModernTargets()
        {
            var innerException = new InvalidOperationException("inner");

            Assert.IsNotNull(new ActivationException());
            Assert.AreEqual("message", new ActivationException("message").Message);
            Assert.AreSame(
                innerException,
                new ActivationException("message", innerException).InnerException);
        }

        private static ConstructorInfo[] GetSerializationConstructors(Type type)
        {
            return type
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(constructor =>
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType.FullName == typeof(SerializationInfo).FullName &&
                           parameters[1].ParameterType.FullName == typeof(StreamingContext).FullName;
                })
                .ToArray();
        }

        private static string GetConstructorSignature(ConstructorInfo constructor)
        {
            return "(" + string.Join(
                ",",
                constructor.GetParameters().Select(parameter => parameter.ParameterType.FullName)) + ")";
        }

        private static void InspectAssembly(string targetFramework, Action<Type> assertion)
        {
            string assemblyPath = GetLibraryAssemblyPath(targetFramework);
            Assert.IsTrue(File.Exists(assemblyPath), "Missing {0} output at {1}", targetFramework, assemblyPath);

            var loadContext = new AssemblyLoadContext(
                "CommonServiceLocator contract inspection: " + targetFramework,
                isCollectible: true);
            try
            {
                Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                Type exceptionType = assembly.GetType(
                    "CommonServiceLocator.ActivationException",
                    throwOnError: true);
                assertion(exceptionType);
            }
            finally
            {
                loadContext.Unload();
            }
        }

        private static string GetLibraryAssemblyPath(string targetFramework)
        {
            string repositoryRoot = FindRepositoryRoot();
            string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
            return Path.GetFullPath(Path.Combine(
                repositoryRoot,
                "src",
                "bin",
                configuration,
                targetFramework,
                "CommonServiceLocator.dll"));
        }

        private static string FindRepositoryRoot()
        {
            for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory != null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "package.props")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("Could not locate the repository root from " + AppContext.BaseDirectory);
            return null;
        }
    }
}
#endif
