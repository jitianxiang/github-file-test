// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using CommonServiceLocator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceLocation.Tests.Components;

namespace ServiceLocation.Tests
{
    [TestClass]
    public class ServiceLocatorImplBaseFixture
    {
        [TestMethod]
        public void GetAllInstances_WrapsDeferredEnumerationFailure()
        {
            var first = new SimpleLogger();
            var failure = new InvalidOperationException("deferred boom");
            var source = new YieldThenThrowSequence(first, failure);
            var locator = new DeferredSequenceServiceLocator(source);

            IEnumerable<object> instances = locator.GetAllInstances(typeof(ILogger));

            Assert.AreEqual(0, source.MoveNextCallCount);

            using (IEnumerator<object> enumerator = instances.GetEnumerator())
            {
                Assert.AreEqual(0, source.MoveNextCallCount);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreSame(first, enumerator.Current);
                Assert.AreEqual(1, source.MoveNextCallCount);

                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                Assert.AreEqual(2, source.MoveNextCallCount);
                Assert.AreEqual(
                    "Activation error occurred while trying to get all instances of type ILogger",
                    actual.Message);
                Assert.AreSame(failure, actual.InnerException);
            }
        }

        [TestMethod]
        public void GetAllInstancesOfT_WrapsDeferredEnumerationFailure()
        {
            var first = new SimpleLogger();
            var failure = new InvalidOperationException("deferred boom");
            var source = new YieldThenThrowSequence(first, failure);
            var locator = new DeferredSequenceServiceLocator(source);

            IEnumerable<ILogger> instances = locator.GetAllInstances<ILogger>();

            Assert.AreEqual(0, source.MoveNextCallCount);

            using (IEnumerator<ILogger> enumerator = instances.GetEnumerator())
            {
                Assert.AreEqual(0, source.MoveNextCallCount);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreSame(first, enumerator.Current);
                Assert.AreEqual(1, source.MoveNextCallCount);

                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                Assert.AreEqual(2, source.MoveNextCallCount);
                Assert.AreEqual(
                    "Activation error occurred while trying to get all instances of type ILogger",
                    actual.Message);
                Assert.AreSame(failure, actual.InnerException);
            }
        }

        [TestMethod]
        public void DeferredEnumerationFailure_UsesCustomAllInstancesFormatter()
        {
            const string customMessage = "custom deferred activation message";
            var failure = new InvalidOperationException("deferred boom");
            var source = new YieldThenThrowSequence(new SimpleLogger(), failure);
            var locator = new FormattingServiceLocator(source, customMessage);

            using (IEnumerator<ILogger> enumerator = locator.GetAllInstances<ILogger>().GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());

                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                Assert.AreEqual(customMessage, actual.Message);
                Assert.AreEqual(1, locator.FormatterCallCount);
                Assert.AreEqual(typeof(ILogger), locator.FormattedServiceType);
                Assert.AreSame(failure, locator.FormattedException);
                Assert.AreSame(failure, actual.InnerException);
            }
        }

        private class DeferredSequenceServiceLocator : ServiceLocatorImplBase
        {
            private readonly IEnumerable<object> _instances;

            public DeferredSequenceServiceLocator(IEnumerable<object> instances)
            {
                _instances = instances;
            }

            protected override object DoGetInstance(Type serviceType, string key)
            {
                throw new NotSupportedException();
            }

            protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
            {
                return _instances;
            }
        }

        private sealed class FormattingServiceLocator : DeferredSequenceServiceLocator
        {
            private readonly string _message;

            public FormattingServiceLocator(IEnumerable<object> instances, string message)
                : base(instances)
            {
                _message = message;
            }

            public int FormatterCallCount { get; private set; }

            public Exception FormattedException { get; private set; }

            public Type FormattedServiceType { get; private set; }

            protected override string FormatActivateAllExceptionMessage(Exception actualException, Type serviceType)
            {
                FormatterCallCount++;
                FormattedException = actualException;
                FormattedServiceType = serviceType;
                return _message;
            }
        }

        private sealed class YieldThenThrowSequence : IEnumerable<object>
        {
            private readonly object _first;
            private readonly Exception _failure;

            public YieldThenThrowSequence(object first, Exception failure)
            {
                _first = first;
                _failure = failure;
            }

            public int MoveNextCallCount { get; private set; }

            public IEnumerator<object> GetEnumerator()
            {
                return Enumerate().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private IEnumerable<object> Enumerate()
            {
                MoveNextCallCount++;
                yield return _first;

                MoveNextCallCount++;
                throw _failure;
            }
        }
    }
}
