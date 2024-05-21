using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Imported.PeanutButter.Utils;
using PeanutButter.XmlUtils;

namespace find_orphaned_code_files
{
    public class UnsupportedProjectException : NotSupportedException
    {
        public bool IsValidModernCsProj { get; }

        public UnsupportedProjectException(
            string fileName,
            bool isValidModernCsProj) : this(fileName, isValidModernCsProj, null)
        {
        }

        public UnsupportedProjectException(
            string fileName,
            bool isValidModernCsProj,
            Exception innerException) : base(GenerateMessageFor(fileName), innerException)
        {
            IsValidModernCsProj = isValidModernCsProj;
        }

        private static string GenerateMessageFor(string fileName)
        {
            return $"{fileName} is not supported by this tool (must be an old-school .csproj)";
        }
    }

    public class CsProjFileReader
    {
        public string[] CompiledFiles =>
            _includedFiles ?? (_includedFiles = FindIncludedFilesOfType("Compile"));

        public string[] ContentFiles =>
            _contentFiles ?? (_contentFiles = FindIncludedFilesOfType("Content"));

        private XDocument _doc;
        private string[] _includedFiles;
        private XNamespace _xmlNamespace;
        private XPathNavigator _navigator;
        private string[] _contentFiles;
        private string _fileName;

        public CsProjFileReader(
            string fileName
        )
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            Parse(fileName);
        }

        private void Parse(string fileName)
        {
            try
            {
                _fileName = fileName;
                _doc = XDocument.Load(fileName);
                if (_doc.Root == null)
                {
                    throw new FileLoadException($"{fileName} has no root element");
                }

                _xmlNamespace = _doc.Root.GetDefaultNamespace();
                _navigator = _doc.Root.CreateNavigator();
            }
            catch (Exception ex)
            {
                throw new UnsupportedProjectException(
                    fileName,
                    false,
                    ex);
            }

            _doc.ScrubNamespaces();
            var projectNode = _doc.XPathSelectElement("/Project", _navigator);
            if (projectNode == null)
            {
                throw new UnsupportedProjectException(fileName, false);
            }

            if (projectNode.Attribute("ToolsVersion") != null)
            {
                return;
            }

            throw new UnsupportedProjectException(
                fileName,
                projectNode.Attribute("Sdk") != null);
        }

        private string[] FindIncludedFilesOfType(string type)
        {
            return FindFileNodesOfType(type)
                .Select(n => n.Attribute("Include")?.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
        }

        private XElement[] FindFileNodesOfType(string type)
        {
            return _doc
                .XPathSelectElements($"/Project/ItemGroup/{type}")
                .ToArray();
        }

        public void RemoveFilesOfType(
            string type,
            Func<string, bool> selector,
            Func<string, bool> reporter)
        {
            var nodes = FindFileNodesOfType(type)
                .Where(el => el.Attribute("Include") != null)
                .Where(el => selector(el.Attribute("Include").Value));
            nodes.ForEach(node =>
            {
                if (!reporter(node.Attribute("Include").Value))
                {
                    return;
                }
                node.Remove();
            });
        }

        public void Persist()
        {
            using (var outStream = File.Open(_fileName, FileMode.Truncate, FileAccess.Write))
            {
                _doc.WriteTo(
                    XmlWriter.Create(outStream)
                );
            }
        }
    }
}