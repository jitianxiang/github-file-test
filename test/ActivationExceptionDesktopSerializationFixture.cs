// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

#if NETFRAMEWORK
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using CommonServiceLocator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ServiceLocation.Tests
{
    [TestClass]
    public class ActivationExceptionDesktopSerializationFixture
    {
        [TestMethod]
        public void BinaryFormatterRoundTripPreservesCompleteExceptionState()
        {
            ActivationException original = CreatePopulatedException();
            ActivationException deserialized;

            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, original);
                stream.Position = 0;
                deserialized = (ActivationException)formatter.Deserialize(stream);
            }

            Assert.AreEqual(original.GetType(), deserialized.GetType());
            Assert.AreEqual(original.Message, deserialized.Message);
            Assert.AreEqual(original.InnerException.GetType(), deserialized.InnerException.GetType());
            Assert.AreEqual(original.InnerException.Message, deserialized.InnerException.Message);
            Assert.AreEqual(original.Data.Count, deserialized.Data.Count);
            Assert.AreEqual(original.Data["detail"], deserialized.Data["detail"]);
            Assert.AreEqual(original.Data["number"], deserialized.Data["number"]);
            Assert.AreEqual(original.HelpLink, deserialized.HelpLink);
            Assert.AreEqual(original.Source, deserialized.Source);
            Assert.AreEqual(original.HResult, deserialized.HResult);
            Assert.AreEqual(original.StackTrace, deserialized.StackTrace);
        }

        [TestMethod]
        public void ExistingConstructorsRemainUsableOnDesktop()
        {
            var innerException = new InvalidOperationException("inner");

            Assert.IsNotNull(new ActivationException());
            Assert.AreEqual("message", new ActivationException("message").Message);
            Assert.AreSame(
                innerException,
                new ActivationException("message", innerException).InnerException);
        }

        private static ActivationException CreatePopulatedException()
        {
            var exception = new ActivationException(
                "activation failed",
                new InvalidOperationException("inner failure"))
            {
                HelpLink = "https://example.test/activation",
                Source = "desktop serialization test"
            };
            exception.Data["detail"] = "serialized data";
            exception.Data["number"] = 42;

            PropertyInfo hResult = typeof(Exception).GetProperty(
                "HResult",
                BindingFlags.Instance | BindingFlags.Public);
            hResult.SetValue(exception, unchecked((int)0x81234567), null);

            try
            {
                throw exception;
            }
            catch (ActivationException caught)
            {
                return caught;
            }
        }
    }
}
#endif
