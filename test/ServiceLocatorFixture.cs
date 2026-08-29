// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CommonServiceLocator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceLocation.Tests.Mocks;

namespace ServiceLocation.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ServiceLocatorFixture
    {
        private const string MissingProviderMessage = " ServiceLocationProvider must be set.";

        [TestInitialize]
        public void TestInit()
        {
            ServiceLocator.SetLocatorProvider(null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ServiceLocator.SetLocatorProvider(null);
        }

        [TestMethod]
        public void ServiceLocatorIsLocationProviderSetReturnsTrueWhenSet()
        {
            ServiceLocator.SetLocatorProvider(() => CreateLocator());

            Assert.IsTrue(ServiceLocator.IsLocationProviderSet);
        }

        [TestMethod]
        public void ServiceLocatorIsLocationProviderSetReturnsFalseWhenNotSet()
        {
            Assert.IsFalse(ServiceLocator.IsLocationProviderSet);
        }

        [TestMethod]
        public void ServiceLocatorCurrentThrowsWhenLocationProviderNotSet()
        {
            InvalidOperationException actual = Assert.ThrowsException<InvalidOperationException>(
                () => { var unused = ServiceLocator.Current; });

            Assert.AreEqual(MissingProviderMessage, actual.Message);
        }

        [TestMethod]
        public void PushLocatorProvider_RestoresUnsetProvider()
        {
            IServiceLocator scopedLocator = CreateLocator();

            using (ServiceLocator.PushLocatorProvider(() => scopedLocator))
            {
                Assert.IsTrue(ServiceLocator.IsLocationProviderSet);
                Assert.AreSame(scopedLocator, ServiceLocator.Current);
            }

            Assert.IsFalse(ServiceLocator.IsLocationProviderSet);
            Assert.ThrowsException<InvalidOperationException>(
                () => { var unused = ServiceLocator.Current; });
        }

        [TestMethod]
        public void PushLocatorProvider_RestoresExistingProvider()
        {
            IServiceLocator firstOriginalLocator = CreateLocator();
            IServiceLocator secondOriginalLocator = CreateLocator();
            IServiceLocator scopedLocator = CreateLocator();
            int originalProviderCalls = 0;
            ServiceLocatorProvider originalProvider = () =>
                ++originalProviderCalls == 1 ? firstOriginalLocator : secondOriginalLocator;
            ServiceLocator.SetLocatorProvider(originalProvider);

            using (ServiceLocator.PushLocatorProvider(() => scopedLocator))
            {
                Assert.AreEqual(0, originalProviderCalls);
                Assert.AreSame(scopedLocator, ServiceLocator.Current);
            }

            Assert.AreEqual(0, originalProviderCalls);
            Assert.AreSame(firstOriginalLocator, ServiceLocator.Current);
            Assert.AreSame(secondOriginalLocator, ServiceLocator.Current);
            Assert.AreEqual(2, originalProviderCalls);
        }

        [TestMethod]
        public void PushLocatorProvider_InvokesProviderForEveryCurrentAccess()
        {
            IServiceLocator firstLocator = CreateLocator();
            IServiceLocator secondLocator = CreateLocator();
            int callCount = 0;

            using (ServiceLocator.PushLocatorProvider(
                () => ++callCount == 1 ? firstLocator : secondLocator))
            {
                Assert.AreEqual(0, callCount);
                Assert.AreSame(firstLocator, ServiceLocator.Current);
                Assert.AreSame(secondLocator, ServiceLocator.Current);
                Assert.AreEqual(2, callCount);
            }

            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public void PushLocatorProvider_SupportsNestedAndRepeatedDisposal()
        {
            IServiceLocator originalLocator = CreateLocator();
            IServiceLocator outerLocator = CreateLocator();
            IServiceLocator innerLocator = CreateLocator();
            ServiceLocator.SetLocatorProvider(() => originalLocator);

            IDisposable outerScope = ServiceLocator.PushLocatorProvider(() => outerLocator);
            IDisposable innerScope = ServiceLocator.PushLocatorProvider(() => innerLocator);

            Assert.AreSame(innerLocator, ServiceLocator.Current);

            innerScope.Dispose();
            Assert.AreSame(outerLocator, ServiceLocator.Current);

            innerScope.Dispose();
            Assert.AreSame(outerLocator, ServiceLocator.Current);

            outerScope.Dispose();
            Assert.AreSame(originalLocator, ServiceLocator.Current);

            outerScope.Dispose();
            Assert.AreSame(originalLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void PushLocatorProvider_OutOfOrderDisposalSkipsDisposedOuterScopes()
        {
            IServiceLocator originalLocator = CreateLocator();
            IServiceLocator outerLocator = CreateLocator();
            IServiceLocator middleLocator = CreateLocator();
            IServiceLocator innerLocator = CreateLocator();
            int disposedProviderCalls = 0;
            ServiceLocator.SetLocatorProvider(() => originalLocator);

            IDisposable outerScope = ServiceLocator.PushLocatorProvider(() =>
            {
                Interlocked.Increment(ref disposedProviderCalls);
                return outerLocator;
            });
            IDisposable middleScope = ServiceLocator.PushLocatorProvider(() =>
            {
                Interlocked.Increment(ref disposedProviderCalls);
                return middleLocator;
            });
            IDisposable innerScope = ServiceLocator.PushLocatorProvider(() => innerLocator);

            outerScope.Dispose();
            middleScope.Dispose();
            Assert.AreSame(innerLocator, ServiceLocator.Current);

            innerScope.Dispose();
            Assert.AreSame(originalLocator, ServiceLocator.Current);
            Assert.AreEqual(0, disposedProviderCalls);

            outerScope.Dispose();
            middleScope.Dispose();
            innerScope.Dispose();
            Assert.AreSame(originalLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void SetLocatorProvider_InvalidatesExistingScopes()
        {
            IServiceLocator outerLocator = CreateLocator();
            IServiceLocator innerLocator = CreateLocator();
            IServiceLocator explicitlySetLocator = CreateLocator();
            IServiceLocator laterScopedLocator = CreateLocator();

            IDisposable outerScope = ServiceLocator.PushLocatorProvider(() => outerLocator);
            IDisposable innerScope = ServiceLocator.PushLocatorProvider(() => innerLocator);

            ServiceLocator.SetLocatorProvider(() => explicitlySetLocator);

            IDisposable laterScope = ServiceLocator.PushLocatorProvider(() => laterScopedLocator);
            innerScope.Dispose();
            outerScope.Dispose();
            Assert.AreSame(laterScopedLocator, ServiceLocator.Current);

            laterScope.Dispose();
            Assert.AreSame(explicitlySetLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void SetLocatorProviderWithNull_InvalidatesExistingScopes()
        {
            IDisposable scope = ServiceLocator.PushLocatorProvider(() => CreateLocator());

            ServiceLocator.SetLocatorProvider(null);
            scope.Dispose();

            Assert.IsFalse(ServiceLocator.IsLocationProviderSet);
            InvalidOperationException actual = Assert.ThrowsException<InvalidOperationException>(
                () => { var unused = ServiceLocator.Current; });
            Assert.AreEqual(MissingProviderMessage, actual.Message);
        }

        [TestMethod]
        public void PushLocatorProvider_RestoresProviderWhenUsingBodyThrows()
        {
            IServiceLocator originalLocator = CreateLocator();
            IServiceLocator scopedLocator = CreateLocator();
            var failure = new ApplicationException("using body failed");
            ServiceLocator.SetLocatorProvider(() => originalLocator);

            ApplicationException actual = Assert.ThrowsException<ApplicationException>(() =>
            {
                using (ServiceLocator.PushLocatorProvider(() => scopedLocator))
                {
                    Assert.AreSame(scopedLocator, ServiceLocator.Current);
                    throw failure;
                }
            });

            Assert.AreSame(failure, actual);
            Assert.AreSame(originalLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void PushLocatorProvider_RejectsNullWithoutChangingState()
        {
            IServiceLocator originalLocator = CreateLocator();
            ServiceLocator.SetLocatorProvider(() => originalLocator);

            ArgumentNullException actual = Assert.ThrowsException<ArgumentNullException>(
                () => ServiceLocator.PushLocatorProvider(null));

            Assert.AreEqual("newProvider", actual.ParamName);
            Assert.IsTrue(ServiceLocator.IsLocationProviderSet);
            Assert.AreSame(originalLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void Current_PreservesProviderExceptionIdentity()
        {
            var failure = new ApplicationException("provider failed");

            using (ServiceLocator.PushLocatorProvider(() => throw failure))
            {
                ApplicationException actual = Assert.ThrowsException<ApplicationException>(
                    () => { var unused = ServiceLocator.Current; });

                Assert.AreSame(failure, actual);
            }
        }

        [TestMethod]
        public void Current_PreservesNullProviderResult()
        {
            using (ServiceLocator.PushLocatorProvider(() => null))
            {
                Assert.IsTrue(ServiceLocator.IsLocationProviderSet);
                Assert.IsNull(ServiceLocator.Current);
            }
        }

        [TestMethod]
        public void PushLocatorProvider_DoesNotOwnProviderOrReturnedLocator()
        {
            var locator = new DisposableMockServiceLocator();
            var provider = new DisposableProvider(locator);

            IDisposable scope = ServiceLocator.PushLocatorProvider(provider.GetLocator);

            Assert.AreEqual(0, provider.CallCount);
            Assert.AreSame(locator, ServiceLocator.Current);
            Assert.AreEqual(1, provider.CallCount);

            scope.Dispose();
            scope.Dispose();

            Assert.AreEqual(1, provider.CallCount);
            Assert.IsFalse(provider.IsDisposed);
            Assert.IsFalse(locator.IsDisposed);
        }

        [TestMethod]
        public void PushLocatorProvider_IsProcessWide()
        {
            IServiceLocator originalLocator = CreateLocator();
            IServiceLocator scopedLocator = CreateLocator();
            ServiceLocator.SetLocatorProvider(() => originalLocator);

            IDisposable scope = Task.Run(
                () => ServiceLocator.PushLocatorProvider(() => scopedLocator))
                .GetAwaiter()
                .GetResult();

            Assert.AreSame(scopedLocator, ServiceLocator.Current);

            scope.Dispose();
            Assert.AreSame(originalLocator, ServiceLocator.Current);
        }

        [TestMethod]
        public void Current_InvokesCapturedProviderOutsideSynchronization()
        {
            IServiceLocator capturedLocator = CreateLocator();
            IServiceLocator replacementLocator = CreateLocator();
            var providerEntered = new ManualResetEventSlim(false);
            var allowProviderToReturn = new ManualResetEventSlim(false);
            Task<IServiceLocator> read = null;
            Task transition = null;

            try
            {
                ServiceLocator.SetLocatorProvider(() =>
                {
                    providerEntered.Set();
                    allowProviderToReturn.Wait();
                    return capturedLocator;
                });

                read = Task.Run(() => ServiceLocator.Current);
                Assert.IsTrue(
                    providerEntered.Wait(TimeSpan.FromSeconds(5)),
                    "The provider was not invoked in time.");

                transition = Task.Run(
                    () => ServiceLocator.SetLocatorProvider(() => replacementLocator));
                bool transitionCompleted = transition.Wait(TimeSpan.FromSeconds(5));

                allowProviderToReturn.Set();
                IServiceLocator readResult = read.GetAwaiter().GetResult();
                transition.GetAwaiter().GetResult();

                Assert.IsTrue(
                    transitionCompleted,
                    "SetLocatorProvider was blocked while Current invoked its provider.");
                Assert.AreSame(capturedLocator, readResult);
                Assert.AreSame(replacementLocator, ServiceLocator.Current);
            }
            finally
            {
                allowProviderToReturn.Set();
                if (read != null) read.Wait(TimeSpan.FromSeconds(5));
                if (transition != null) transition.Wait(TimeSpan.FromSeconds(5));
                providerEntered.Dispose();
                allowProviderToReturn.Dispose();
            }
        }

        [TestMethod]
        public void ConcurrentReadsAndTransitions_ReturnOnlyValidResultsOrMissingProvider()
        {
            IServiceLocator firstLocator = CreateLocator();
            IServiceLocator secondLocator = CreateLocator();
            IServiceLocator thirdLocator = CreateLocator();
            ServiceLocatorProvider firstProvider = () => firstLocator;
            ServiceLocatorProvider secondProvider = () => secondLocator;
            ServiceLocatorProvider thirdProvider = () => thirdLocator;
            var failures = new ConcurrentQueue<string>();
            var start = new ManualResetEventSlim(false);
            int successfulReads = 0;
            const int readerCount = 4;
            const int readIterations = 30000;
            const int transitionIterations = 12000;
            var tasks = new Task[readerCount + 1];

            ServiceLocator.SetLocatorProvider(firstProvider);

            for (int readerIndex = 0; readerIndex < readerCount; readerIndex++)
            {
                tasks[readerIndex] = Task.Run(() =>
                {
                    start.Wait();

                    for (int iteration = 0; iteration < readIterations; iteration++)
                    {
                        try
                        {
                            IServiceLocator current = ServiceLocator.Current;
                            if (!ReferenceEquals(current, firstLocator)
                                && !ReferenceEquals(current, secondLocator)
                                && !ReferenceEquals(current, thirdLocator))
                            {
                                failures.Enqueue("Current returned an unknown locator.");
                            }
                            else
                            {
                                Interlocked.Increment(ref successfulReads);
                            }
                        }
                        catch (InvalidOperationException exception)
                        {
                            if (exception.Message != MissingProviderMessage)
                            {
                                failures.Enqueue("Unexpected InvalidOperationException: " + exception.Message);
                            }
                        }
                        catch (Exception exception)
                        {
                            failures.Enqueue(exception.GetType().FullName + ": " + exception.Message);
                        }
                    }
                });
            }

            tasks[readerCount] = Task.Run(() =>
            {
                start.Wait();

                for (int iteration = 0; iteration < transitionIterations; iteration++)
                {
                    switch (iteration % 5)
                    {
                        case 0:
                            ServiceLocator.SetLocatorProvider(firstProvider);
                            break;
                        case 1:
                            ServiceLocator.PushLocatorProvider(secondProvider).Dispose();
                            break;
                        case 2:
                            ServiceLocator.SetLocatorProvider(null);
                            break;
                        case 3:
                            IDisposable outer = ServiceLocator.PushLocatorProvider(firstProvider);
                            IDisposable inner = ServiceLocator.PushLocatorProvider(secondProvider);
                            outer.Dispose();
                            inner.Dispose();
                            break;
                        default:
                            IDisposable stale = ServiceLocator.PushLocatorProvider(secondProvider);
                            ServiceLocator.SetLocatorProvider(thirdProvider);
                            stale.Dispose();
                            break;
                    }

                    if ((iteration & 31) == 0) Thread.Yield();
                }
            });

            start.Set();
            try
            {
                Assert.IsTrue(
                    Task.WaitAll(tasks, TimeSpan.FromSeconds(30)),
                    "Concurrent provider operations did not finish in time.");
            }
            finally
            {
                start.Dispose();
            }

            string failureMessage;
            Assert.IsFalse(
                failures.TryPeek(out failureMessage),
                failureMessage ?? "An unexpected concurrent result was observed.");
            Assert.IsTrue(successfulReads > 0);
        }

        private static IServiceLocator CreateLocator()
        {
            return new MockServiceLocator(new object[0]);
        }

        private sealed class DisposableProvider : IDisposable
        {
            private readonly IServiceLocator _locator;

            public DisposableProvider(IServiceLocator locator)
            {
                _locator = locator;
            }

            public int CallCount { get; private set; }

            public bool IsDisposed { get; private set; }

            public IServiceLocator GetLocator()
            {
                CallCount++;
                return _locator;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class DisposableMockServiceLocator : MockServiceLocator, IDisposable
        {
            public DisposableMockServiceLocator()
                : base(new object[0])
            {
            }

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
