// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using CommonServiceLocator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ServiceLocation.Tests.Components;

namespace ServiceLocation.Tests
{
    [TestClass]
    public class ServiceLocatorGetInstanceFixture
    {
        [TestMethod]
        public void AllSingleResolutionPaths_RejectIncompatibleResults()
        {
            AssertInvalidResultFromAllEntryPoints(new object());
        }

        [TestMethod]
        public void AllSingleResolutionPaths_RejectNullReferenceResults()
        {
            AssertInvalidResultFromAllEntryPoints(null);
        }

        [TestMethod]
        public void GetInstance_RejectsNullForNullableAndNonNullableValueTypes()
        {
            AssertInvalidResult(
                null,
                locator => locator.GetInstance(typeof(int?)),
                typeof(int?),
                null);
            AssertInvalidResult(
                null,
                locator => locator.GetInstance<int?>(),
                typeof(int?),
                null);
            AssertInvalidResult(
                null,
                locator => locator.GetInstance(typeof(int)),
                typeof(int),
                null);
            AssertInvalidResult(
                null,
                locator => locator.GetInstance<int>(),
                typeof(int),
                null);
        }

        [TestMethod]
        public void InvalidResult_UsesCustomFormatterWithOriginalArgumentsAndDiagnostic()
        {
            const string customMessage = "custom single activation message";
            string key = new string(new[] { 'u', 'n', 'i', 'q', 'u', 'e' });
            var locator = new FormattingServiceLocator(new object(), customMessage);

            ActivationException actual = Assert.ThrowsException<ActivationException>(
                () => locator.GetInstance(typeof(ILogger), key));

            Assert.AreEqual(customMessage, actual.Message);
            Assert.AreEqual(1, locator.FormatterCallCount);
            Assert.AreEqual(typeof(ILogger), locator.FormattedServiceType);
            Assert.AreSame(key, locator.RequestedKey);
            Assert.AreSame(key, locator.FormattedKey);
            Assert.AreEqual(1, locator.GetInstanceCallCount);
            Assert.IsInstanceOfType(actual.InnerException, typeof(InvalidCastException));
            Assert.AreSame(actual.InnerException, locator.FormattedException);
            Assert.IsFalse(actual.InnerException is ActivationException);
        }

        [TestMethod]
        public void InvalidResult_PreservesNullAndEmptyKeys()
        {
            var nullKeyLocator = new FormattingServiceLocator((object)null, "null key failure");
            string emptyKey = new string('x', 0);
            var emptyKeyLocator = new FormattingServiceLocator((object)null, "empty key failure");

            ActivationException nullKeyFailure = Assert.ThrowsException<ActivationException>(
                () => nullKeyLocator.GetInstance(typeof(ILogger), null));
            ActivationException emptyKeyFailure = Assert.ThrowsException<ActivationException>(
                () => emptyKeyLocator.GetInstance(typeof(ILogger), emptyKey));

            Assert.IsNull(nullKeyLocator.RequestedKey);
            Assert.IsNull(nullKeyLocator.FormattedKey);
            Assert.AreSame(emptyKey, emptyKeyLocator.RequestedKey);
            Assert.AreSame(emptyKey, emptyKeyLocator.FormattedKey);
            Assert.AreEqual("null key failure", nullKeyFailure.Message);
            Assert.AreEqual("empty key failure", emptyKeyFailure.Message);
            Assert.AreSame(nullKeyFailure.InnerException, nullKeyLocator.FormattedException);
            Assert.AreSame(emptyKeyFailure.InnerException, emptyKeyLocator.FormattedException);
        }

        [TestMethod]
        public void GetInstance_AcceptsClrAssignableReferenceResults()
        {
            var interfaceImplementation = new SimpleLogger();
            var subclass = new SpecializedLogger();
            var covariant = new List<string> { "value" };

            Assert.AreSame(
                interfaceImplementation,
                new RecordingServiceLocator(interfaceImplementation).GetInstance(typeof(ILogger)));
            Assert.AreSame(
                interfaceImplementation,
                new RecordingServiceLocator(interfaceImplementation).GetInstance<ILogger>());
            Assert.AreSame(
                subclass,
                new RecordingServiceLocator(subclass).GetInstance(typeof(SimpleLogger)));
            Assert.AreSame(
                subclass,
                new RecordingServiceLocator(subclass).GetInstance<SimpleLogger>());
            Assert.AreSame(
                covariant,
                new RecordingServiceLocator(covariant).GetInstance(typeof(IEnumerable<object>)));
            Assert.AreSame(
                covariant,
                new RecordingServiceLocator(covariant).GetInstance<IEnumerable<object>>());
        }

        [TestMethod]
        public void GetInstance_AcceptsCompatibleBoxedAndNullableValues()
        {
            object boxedValue = 42;
            object boxedNullableValue = (int?)43;

            Assert.AreSame(
                boxedValue,
                new RecordingServiceLocator(boxedValue).GetInstance(typeof(int)));
            Assert.AreEqual(
                42,
                new RecordingServiceLocator(boxedValue).GetInstance<int>());
            Assert.AreSame(
                boxedNullableValue,
                new RecordingServiceLocator(boxedNullableValue).GetInstance(typeof(int?)));
            Assert.AreEqual(
                (int?)43,
                new RecordingServiceLocator(boxedNullableValue).GetInstance<int?>());
        }

        [TestMethod]
        public void GetInstance_RejectsNumericAndUserDefinedConversions()
        {
            AssertInvalidResult(
                (short)42,
                locator => locator.GetInstance(typeof(int)),
                typeof(int),
                null);
            AssertInvalidResult(
                (short)42,
                locator => locator.GetInstance<int>(),
                typeof(int),
                null);
            AssertInvalidResult(
                new ConvertibleToLogger(),
                locator => locator.GetInstance(typeof(SimpleLogger)),
                typeof(SimpleLogger),
                null);
        }

        [TestMethod]
        public void InvalidResult_DoesNotRetryResolutionHook()
        {
            var compatibleResult = new SimpleLogger();
            int resolverCallCount = 0;
            var locator = new RecordingServiceLocator((serviceType, key) =>
            {
                resolverCallCount++;
                return resolverCallCount == 1 ? new object() : compatibleResult;
            });

            ActivationException actual = Assert.ThrowsException<ActivationException>(
                () => locator.GetInstance<ILogger>());

            AssertValidationFailure(
                actual,
                typeof(ILogger),
                null);
            Assert.AreEqual(1, resolverCallCount);
            Assert.AreEqual(1, locator.GetInstanceCallCount);
        }

        [TestMethod]
        public void HookThrownException_PreservesWrappingFormattingAndIdentity()
        {
            const string customMessage = "custom hook failure";
            string key = new string(new[] { 'h', 'o', 'o', 'k' });
            var failure = new InvalidOperationException("resolution failed");
            var locator = new FormattingServiceLocator(
                (serviceType, requestedKey) => throw failure,
                customMessage);

            ActivationException actual = Assert.ThrowsException<ActivationException>(
                () => locator.GetInstance(typeof(ILogger), key));

            Assert.AreEqual(customMessage, actual.Message);
            Assert.AreSame(failure, actual.InnerException);
            Assert.AreSame(failure, locator.FormattedException);
            Assert.AreEqual(typeof(ILogger), locator.FormattedServiceType);
            Assert.AreSame(key, locator.RequestedKey);
            Assert.AreSame(key, locator.FormattedKey);
            Assert.AreEqual(1, locator.GetInstanceCallCount);
            Assert.AreEqual(1, locator.FormatterCallCount);
        }

        [TestMethod]
        public void GetInstance_WithNullServiceTypePreservesSuccessfulHookResults()
        {
            var result = new object();
            var resultLocator = new RecordingServiceLocator(result);
            var nullLocator = new RecordingServiceLocator((object)null);

            Assert.AreSame(result, resultLocator.GetInstance(null, "key"));
            Assert.IsNull(nullLocator.GetInstance(null, "key"));
            Assert.AreEqual(1, resultLocator.GetInstanceCallCount);
            Assert.AreEqual(1, nullLocator.GetInstanceCallCount);
        }

        private static void AssertInvalidResultFromAllEntryPoints(object result)
        {
            const string key = "named";

            AssertInvalidResult(
                result,
                locator => locator.GetInstance(typeof(ILogger), key),
                typeof(ILogger),
                key);
            AssertInvalidResult(
                result,
                locator => locator.GetInstance(typeof(ILogger)),
                typeof(ILogger),
                null);
            AssertInvalidResult(
                result,
                locator => locator.GetInstance<ILogger>(key),
                typeof(ILogger),
                key);
            AssertInvalidResult(
                result,
                locator => locator.GetInstance<ILogger>(),
                typeof(ILogger),
                null);
            AssertInvalidResult(
                result,
                locator => locator.GetService(typeof(ILogger)),
                typeof(ILogger),
                null);
        }

        private static void AssertInvalidResult(
            object result,
            Func<RecordingServiceLocator, object> resolve,
            Type serviceType,
            string expectedKey)
        {
            var locator = new RecordingServiceLocator(result);

            ActivationException actual = Assert.ThrowsException<ActivationException>(
                () => resolve(locator));

            AssertValidationFailure(actual, serviceType, expectedKey);
            Assert.AreEqual(1, locator.GetInstanceCallCount);
            Assert.AreEqual(serviceType, locator.RequestedServiceType);
            Assert.AreEqual(expectedKey, locator.RequestedKey);
        }

        private static void AssertValidationFailure(
            ActivationException actual,
            Type serviceType,
            string key)
        {
            Assert.AreEqual(
                $"Activation error occurred while trying to get instance of type {serviceType.Name}, key \"{key}\"",
                actual.Message);
            Assert.IsInstanceOfType(actual.InnerException, typeof(InvalidCastException));
            Assert.IsFalse(actual.InnerException is ActivationException);
            Assert.IsNull(actual.InnerException.InnerException);
        }

        private class RecordingServiceLocator : ServiceLocatorImplBase
        {
            private readonly Func<Type, string, object> _resolver;

            public RecordingServiceLocator(object result)
                : this((serviceType, key) => result)
            {
            }

            public RecordingServiceLocator(Func<Type, string, object> resolver)
            {
                _resolver = resolver;
            }

            public int GetInstanceCallCount { get; private set; }

            public Type RequestedServiceType { get; private set; }

            public string RequestedKey { get; private set; }

            protected override object DoGetInstance(Type serviceType, string key)
            {
                GetInstanceCallCount++;
                RequestedServiceType = serviceType;
                RequestedKey = key;
                return _resolver(serviceType, key);
            }

            protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FormattingServiceLocator : RecordingServiceLocator
        {
            private readonly string _message;

            public FormattingServiceLocator(object result, string message)
                : base(result)
            {
                _message = message;
            }

            public FormattingServiceLocator(Func<Type, string, object> resolver, string message)
                : base(resolver)
            {
                _message = message;
            }

            public int FormatterCallCount { get; private set; }

            public Exception FormattedException { get; private set; }

            public Type FormattedServiceType { get; private set; }

            public string FormattedKey { get; private set; }

            protected override string FormatActivationExceptionMessage(
                Exception actualException,
                Type serviceType,
                string key)
            {
                FormatterCallCount++;
                FormattedException = actualException;
                FormattedServiceType = serviceType;
                FormattedKey = key;
                return _message;
            }
        }

        private sealed class SpecializedLogger : SimpleLogger
        {
        }

        private sealed class ConvertibleToLogger
        {
            public static implicit operator SimpleLogger(ConvertibleToLogger value)
            {
                return new SimpleLogger();
            }
        }
    }
}
