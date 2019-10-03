using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using PeanutButter.XmlUtils;

namespace find_orphaned_code_files
{
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
                ThrowNotSupported(fileName, ex);
            }

            _doc.ScrubNamespaces();
            var projectNode = _doc.XPathSelectElement("/Project", _navigator);
            if (projectNode.Attribute("ToolsVersion") == null)
            {
                ThrowNotSupported(fileName);
            }
        }

        private void ThrowNotSupported(string fileName, Exception ex = null)
        {
            var message = $"{fileName} is not supported by this tool (must be an old-school .csproj)";
            if (ex != null)
            {
                message += $"\nMore info: {ex.Message}";
            }

            throw new NotSupportedException(message);
        }

        private string[] FindIncludedFilesOfType(string type)
        {
            return _doc.XPathSelectElements($"/Project/ItemGroup/{type}")
                .Select(n => n.Attribute("Include")?.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
        }
    }
}