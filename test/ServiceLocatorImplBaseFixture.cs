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

        [TestMethod]
        public void GetAllInstances_RejectsIncompatibleElementLazilyAndDisposesSource()
        {
            var first = new SimpleLogger();
            var sentinel = new AdvancedLogger();
            var source = new TrackingSequence(first, new object(), sentinel);
            var locator = new DeferredSequenceServiceLocator(source);

            IEnumerable<object> instances = locator.GetAllInstances(typeof(ILogger));

            Assert.AreEqual(0, source.GetEnumeratorCallCount);
            Assert.AreEqual(0, source.MoveNextCallCount);
            Assert.AreEqual(0, source.DisposeCallCount);

            IEnumerator<object> enumerator = instances.GetEnumerator();
            try
            {
                Assert.AreEqual(0, source.GetEnumeratorCallCount);
                Assert.AreEqual(0, source.MoveNextCallCount);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreSame(first, enumerator.Current);
                Assert.AreEqual(1, source.GetEnumeratorCallCount);
                Assert.AreEqual(1, source.MoveNextCallCount);
                Assert.AreEqual(0, source.DisposeCallCount);

                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                AssertInvalidElementException(actual);
                Assert.AreEqual(2, source.MoveNextCallCount);
                Assert.AreEqual(2, source.CurrentCallCount);
                Assert.AreEqual(1, source.DisposeCallCount);
            }
            finally
            {
                enumerator.Dispose();
            }

            Assert.AreEqual(2, source.MoveNextCallCount);
            Assert.AreEqual(1, source.DisposeCallCount);
        }

        [TestMethod]
        public void GetAllInstancesOfT_RejectsIncompatibleElementLazilyAndDisposesSource()
        {
            var first = new SimpleLogger();
            var sentinel = new AdvancedLogger();
            var source = new TrackingSequence(first, new object(), sentinel);
            var locator = new DeferredSequenceServiceLocator(source);

            IEnumerable<ILogger> instances = locator.GetAllInstances<ILogger>();

            Assert.AreEqual(0, source.GetEnumeratorCallCount);
            Assert.AreEqual(0, source.MoveNextCallCount);
            Assert.AreEqual(0, source.DisposeCallCount);

            IEnumerator<ILogger> enumerator = instances.GetEnumerator();
            try
            {
                Assert.AreEqual(0, source.GetEnumeratorCallCount);
                Assert.AreEqual(0, source.MoveNextCallCount);
                Assert.IsTrue(enumerator.MoveNext());
                Assert.AreSame(first, enumerator.Current);
                Assert.AreEqual(1, source.GetEnumeratorCallCount);
                Assert.AreEqual(1, source.MoveNextCallCount);
                Assert.AreEqual(0, source.DisposeCallCount);

                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                AssertInvalidElementException(actual);
                Assert.AreEqual(2, source.MoveNextCallCount);
                Assert.AreEqual(2, source.CurrentCallCount);
                Assert.AreEqual(1, source.DisposeCallCount);
            }
            finally
            {
                enumerator.Dispose();
            }

            Assert.AreEqual(2, source.MoveNextCallCount);
            Assert.AreEqual(1, source.DisposeCallCount);
        }

        [TestMethod]
        public void GetAllInstances_RejectsNullElement()
        {
            var source = new TrackingSequence(null, new SimpleLogger());
            var locator = new DeferredSequenceServiceLocator(source);

            using (IEnumerator<object> enumerator = locator.GetAllInstances(typeof(ILogger)).GetEnumerator())
            {
                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                AssertInvalidElementException(actual);
                Assert.AreEqual(1, source.MoveNextCallCount);
                Assert.AreEqual(1, source.CurrentCallCount);
                Assert.AreEqual(1, source.DisposeCallCount);
            }

            Assert.AreEqual(1, source.DisposeCallCount);
        }

        [TestMethod]
        public void GetAllInstancesOfT_RejectsNullElement()
        {
            var source = new TrackingSequence(null, new SimpleLogger());
            var locator = new DeferredSequenceServiceLocator(source);

            using (IEnumerator<ILogger> enumerator = locator.GetAllInstances<ILogger>().GetEnumerator())
            {
                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                AssertInvalidElementException(actual);
                Assert.AreEqual(1, source.MoveNextCallCount);
                Assert.AreEqual(1, source.CurrentCallCount);
                Assert.AreEqual(1, source.DisposeCallCount);
            }

            Assert.AreEqual(1, source.DisposeCallCount);
        }

        [TestMethod]
        public void InvalidElement_UsesCustomAllInstancesFormatter()
        {
            const string customMessage = "custom invalid element message";
            var source = new TrackingSequence(new object());
            var locator = new FormattingServiceLocator(source, customMessage);

            using (IEnumerator<ILogger> enumerator = locator.GetAllInstances<ILogger>().GetEnumerator())
            {
                ActivationException actual = Assert.ThrowsException<ActivationException>(
                    () => enumerator.MoveNext());

                Assert.AreEqual(customMessage, actual.Message);
                Assert.AreEqual(1, locator.FormatterCallCount);
                Assert.AreEqual(typeof(ILogger), locator.FormattedServiceType);
                Assert.IsInstanceOfType(actual.InnerException, typeof(InvalidCastException));
                Assert.AreSame(actual.InnerException, locator.FormattedException);
            }
        }

        [TestMethod]
        public void GetAllInstances_AcceptsAssignableReferencesAndCompatibleBoxedValues()
        {
            var interfaceImplementation = new SimpleLogger();
            var subclass = new SpecializedLogger();
            object boxedValue = 42;

            CollectionAssert.AreEqual(
                new object[] { interfaceImplementation },
                new List<object>(new DeferredSequenceServiceLocator(new object[] { interfaceImplementation })
                    .GetAllInstances(typeof(ILogger))));
            CollectionAssert.AreEqual(
                new object[] { subclass },
                new List<object>(new DeferredSequenceServiceLocator(new object[] { subclass })
                    .GetAllInstances(typeof(SimpleLogger))));
            CollectionAssert.AreEqual(
                new object[] { boxedValue },
                new List<object>(new DeferredSequenceServiceLocator(new object[] { boxedValue })
                    .GetAllInstances(typeof(int?))));
        }

        [TestMethod]
        public void GetAllInstancesOfT_AcceptsAssignableReferencesAndCompatibleBoxedValues()
        {
            var interfaceImplementation = new SimpleLogger();
            var subclass = new SpecializedLogger();
            object boxedValue = 42;

            CollectionAssert.AreEqual(
                new ILogger[] { interfaceImplementation },
                new List<ILogger>(new DeferredSequenceServiceLocator(new object[] { interfaceImplementation })
                    .GetAllInstances<ILogger>()));
            CollectionAssert.AreEqual(
                new SimpleLogger[] { subclass },
                new List<SimpleLogger>(new DeferredSequenceServiceLocator(new object[] { subclass })
                    .GetAllInstances<SimpleLogger>()));
            CollectionAssert.AreEqual(
                new int?[] { 42 },
                new List<int?>(new DeferredSequenceServiceLocator(new object[] { boxedValue })
                    .GetAllInstances<int?>()));
        }

        [TestMethod]
        public void GetAllInstances_LeavesEmptySequencesUnchanged()
        {
            var locator = new DeferredSequenceServiceLocator(new object[0]);

            using (IEnumerator<object> untyped = locator.GetAllInstances(typeof(ILogger)).GetEnumerator())
            using (IEnumerator<ILogger> typed = locator.GetAllInstances<ILogger>().GetEnumerator())
            {
                Assert.IsFalse(untyped.MoveNext());
                Assert.IsFalse(typed.MoveNext());
            }
        }

        private static void AssertInvalidElementException(ActivationException actual)
        {
            Assert.AreEqual(
                "Activation error occurred while trying to get all instances of type ILogger",
                actual.Message);
            Assert.IsInstanceOfType(actual.InnerException, typeof(InvalidCastException));
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

        private sealed class SpecializedLogger : SimpleLogger
        {
        }

        private sealed class TrackingSequence : IEnumerable<object>
        {
            private readonly object[] _items;

            public TrackingSequence(params object[] items)
            {
                _items = items;
            }

            public int GetEnumeratorCallCount { get; private set; }

            public int MoveNextCallCount { get; private set; }

            public int CurrentCallCount { get; private set; }

            public int DisposeCallCount { get; private set; }

            public IEnumerator<object> GetEnumerator()
            {
                GetEnumeratorCallCount++;
                return new TrackingEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private sealed class TrackingEnumerator : IEnumerator<object>
            {
                private readonly TrackingSequence _sequence;
                private int _index = -1;

                public TrackingEnumerator(TrackingSequence sequence)
                {
                    _sequence = sequence;
                }

                public object Current
                {
                    get
                    {
                        _sequence.CurrentCallCount++;
                        return _sequence._items[_index];
                    }
                }

                object IEnumerator.Current => Current;

                public bool MoveNext()
                {
                    _sequence.MoveNextCallCount++;
                    _index++;
                    return _index < _sequence._items.Length;
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }

                public void Dispose()
                {
                    _sequence.DisposeCallCount++;
                }
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
