﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for ProjectStringCache</summary>
//-----------------------------------------------------------------------

using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    /// <summary>
    /// Tests for ProjectStringCache
    /// </summary>
    [TestClass]
    public class ProjectStringCache_Tests
    {
        /// <summary>
        /// Test that loading two instances of the same xml file uses the same strings
        /// to store read values.
        /// </summary>
        [TestMethod]
        public void ContentIsSameAcrossInstances()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <ItemGroup>
                           Item group content
                        </ItemGroup>
                    </Project>
                    ");

            string path = FileUtilities.GetTemporaryFile();

            try
            {
                File.WriteAllText(path, content);

                ProjectStringCache cache = new ProjectStringCache();
                XmlDocumentWithLocation document1 = new XmlDocumentWithLocation();
                document1.StringCache = cache;
                document1.Load(path);

                XmlDocumentWithLocation document2 = new XmlDocumentWithLocation();
                document2.StringCache = cache;
                document2.Load(path);

                XmlNodeList nodes1 = document1.GetElementsByTagName("ItemGroup");
                XmlNodeList nodes2 = document2.GetElementsByTagName("ItemGroup");

                Assert.AreEqual(1, nodes1.Count);
                Assert.AreEqual(1, nodes2.Count);

                XmlNode node1 = nodes1[0].FirstChild;
                XmlNode node2 = nodes2[0].FirstChild;

                Assert.IsNotNull(node1);
                Assert.IsNotNull(node2);
                Assert.AreNotSame(node1, node2);
                Assert.AreSame(node1.Value, node2.Value);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Test that modifying one instance of a file does not affect the other file.
        /// </summary>
        [TestMethod]
        public void ContentCanBeModified()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <ItemGroup attr1='attr1value'>
                           Item group content
                        </ItemGroup>
                    </Project>
                    ");

            string path = FileUtilities.GetTemporaryFile();

            try
            {
                File.WriteAllText(path, content);
                ProjectStringCache cache = new ProjectStringCache();
                XmlDocumentWithLocation document1 = new XmlDocumentWithLocation();
                document1.StringCache = cache;
                document1.Load(path);

                XmlDocumentWithLocation document2 = new XmlDocumentWithLocation();
                document2.StringCache = cache;
                document2.Load(path);

                string outerXml1 = document1.OuterXml;
                string outerXml2 = document2.OuterXml;
                Assert.AreEqual(outerXml1, outerXml2);

                XmlNodeList nodes1 = document1.GetElementsByTagName("ItemGroup");
                XmlNodeList nodes2 = document2.GetElementsByTagName("ItemGroup");

                Assert.AreEqual(1, nodes1.Count);
                Assert.AreEqual(1, nodes2.Count);

                XmlNode node1 = nodes1[0];
                XmlNode node2 = nodes2[0];
                Assert.IsNotNull(node1);
                Assert.IsNotNull(node2);
                Assert.AreNotSame(node1, node2);
                Assert.AreEqual(1, node1.Attributes.Count);
                Assert.AreEqual(1, node2.Attributes.Count);
                Assert.AreSame(node1.Attributes[0].Value, node2.Attributes[0].Value);

                node2.Attributes[0].Value = "attr1value";
                Assert.AreEqual(node1.Attributes[0].Value, node2.Attributes[0].Value);
                Assert.AreNotSame(node1.Attributes[0].Value, node2.Attributes[0].Value);

                node1 = nodes1[0].FirstChild;
                node2 = nodes2[0].FirstChild;
                Assert.AreNotSame(node1, node2);
                Assert.AreSame(node1.Value, node2.Value);

                XmlText newText = document2.CreateTextNode("New Value");
                XmlNode parent = node2.ParentNode;
                parent.ReplaceChild(newText, node2);

                Assert.AreNotEqual(outerXml1, document2.OuterXml);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Test that unloading a project file makes its string entries disappear from
        /// the string cache.
        /// </summary>
        [TestMethod]
        public void RemovingFilesRemovesEntries()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <ItemGroup>Content</ItemGroup>
                    </Project>
                    ");

            string path = FileUtilities.GetTemporaryFile();

            try
            {
                File.WriteAllText(path, content);

                ProjectStringCache cache = new ProjectStringCache();
                ProjectCollection collection = new ProjectCollection();
                int entryCount;

                ProjectRootElement pre1 = ProjectRootElement.Create(collection);
                pre1.XmlDocument.StringCache = cache;
                pre1.FullPath = path;
                pre1.XmlDocument.Load(path);
                entryCount = cache.Count;
                Assert.IsTrue(entryCount > 0);

                ProjectRootElement pre2 = ProjectRootElement.Create(collection);
                pre2.XmlDocument.StringCache = cache;
                pre2.FullPath = path;
                pre2.XmlDocument.Load(path);

                // Entry count should not have changed
                Assert.AreEqual(entryCount, cache.Count);

                string itemGroupContent = cache.Get("Content");
                Assert.IsNotNull(itemGroupContent);

                XmlNodeList nodes1 = pre1.XmlDocument.GetElementsByTagName("ItemGroup");
                XmlNodeList nodes2 = pre2.XmlDocument.GetElementsByTagName("ItemGroup");

                Assert.AreEqual(1, nodes1.Count);
                Assert.AreEqual(1, nodes2.Count);

                XmlNode node1 = nodes1[0];
                XmlNode node2 = nodes2[0];
                Assert.IsNotNull(node1);
                Assert.IsNotNull(node2);
                Assert.AreNotSame(node1, node2);
                Assert.AreSame(node1.Value, node2.Value);

                // Now remove one document
                collection.UnloadProject(pre1);

                // We should still be able to get Content
                itemGroupContent = cache.Get("Content");
                Assert.IsNotNull(itemGroupContent);

                // Now remove the second document
                collection.UnloadProject(pre2);

                // Now we should not be able to get Content
                itemGroupContent = cache.Get("Content");
                Assert.IsNull(itemGroupContent);

                // And there should be no entries
                Assert.AreEqual(0, cache.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Adding a string equivalent to an existing instance and under the same document should
        /// return the existing instance.
        /// </summary>
        [TestMethod]
        public void AddReturnsSameInstanceForSameDocument()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);

            // Content of string should be the same.
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(stringToAdd, return1);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("1");

            string return2 = cache.Add(builder.ToString(), document);

            // Content of string should be the same.            
            Assert.AreEqual(builder.ToString(), return2);

            // Returned references should be the same
            Assert.AreSame(return1, return2);

            // Should not have added any new string instances to the cache.
            Assert.AreEqual(1, cache.Count);
        }

        /// <summary>
        /// Adding a string equivalent to an existing instance but under a different document 
        /// should return the existing instance.
        /// </summary>
        [TestMethod]
        public void AddReturnsSameInstanceForDifferentDocument()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);

            // Content of string should be the same.
            Assert.AreEqual(stringToAdd, return1);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("1");
            XmlDocument document2 = new XmlDocument();

            string return2 = cache.Add(builder.ToString(), document2);

            // Content of string should be the same.
            Assert.AreEqual(builder.ToString(), return2);

            // Returned references should be the same
            Assert.AreSame(return1, return2);

            // Should not have added any new string instances to the cache.
            Assert.AreEqual(1, cache.Count);
        }

        /// <summary>
        /// Removing the last document containing an instance of a string should remove the string entry.
        /// A subsequent add should then return a different instance.
        /// </summary>
        /// <remarks>
        /// WHITEBOX ASSUMPTION:
        /// The following method assumes knowledge of the ProjectStringCache internal implementation
        /// details, and may become invalid if those details change.
        /// </remarks>        
        [TestMethod]
        public void RemoveLastInstanceDeallocatesEntry()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);

            cache.Clear(document);

            // Should be no instances left.
            Assert.AreEqual(0, cache.Count);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("1");
            XmlDocument document2 = new XmlDocument();

            string return2 = cache.Add(builder.ToString(), document2);

            // Returned references should NOT be the same
            Assert.AreNotSame(return1, return2);
        }

        /// <summary>
        /// Removing one document containing a string which already existed in the collection 
        /// should still leave a reference in the collection, so that a subsequent add will
        /// return the existing reference.
        /// </summary>
        [TestMethod]
        public void RemoveOneInstance()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);
            Assert.AreEqual(1, cache.Count);

            XmlDocument document2 = new XmlDocument();
            string return2 = cache.Add(stringToAdd, document2);
            Assert.AreEqual(1, cache.Count);

            cache.Clear(document2);

            // Since there is still one document referencing the string, it should remain.
            Assert.AreEqual(1, cache.Count);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("1");
            XmlDocument document3 = new XmlDocument();

            string return3 = cache.Add(builder.ToString(), document3);

            // Returned references should be the same
            Assert.AreSame(return1, return3);

            // Still should only be one cached instance.
            Assert.AreEqual(1, cache.Count);
        }

        /// <summary>
        /// Different strings should get their own entries.
        /// </summary>
        [TestMethod]
        public void DifferentStringsSameDocument()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);
            Assert.AreEqual(1, cache.Count);

            stringToAdd = "Test2";
            string return2 = cache.Add(stringToAdd, document);

            // The second string gets its own instance.
            Assert.AreEqual(2, cache.Count);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("2");
            string return3 = cache.Add(builder.ToString(), document);

            // The new string should be the same as the other one already in the collection.
            Assert.AreSame(return2, return3);

            // No new instances for string with the same content.
            Assert.AreEqual(2, cache.Count);
        }

        /// <summary>
        /// Different strings should get their own entries.
        /// </summary>
        [TestMethod]
        public void DifferentStringsDifferentDocuments()
        {
            ProjectStringCache cache = new ProjectStringCache();

            XmlDocument document = new XmlDocument();

            string stringToAdd = "Test1";
            string return1 = cache.Add(stringToAdd, document);
            Assert.AreEqual(1, cache.Count);

            stringToAdd = "Test2";
            XmlDocument document2 = new XmlDocument();
            string return2 = cache.Add(stringToAdd, document2);

            // The second string gets its own instance.
            Assert.AreEqual(2, cache.Count);

            // Build a new string guaranteed not to be optimized by the compiler into the same instance.
            StringBuilder builder = new StringBuilder();
            builder.Append("Test");
            builder.Append("2");
            XmlDocument document3 = new XmlDocument();
            string return3 = cache.Add(builder.ToString(), document3);

            // The new string should be the same as the other one already in the collection.
            Assert.AreSame(return2, return3);

            // No new instances for string with the same content.
            Assert.AreEqual(2, cache.Count);
        }
    }
}
