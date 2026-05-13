using System.Xml;

namespace NzbDrone.Common.Xml
{
    public static class SafeXmlReaderSettings
    {
        public const long MaxCharactersFromEntities = 1024 * 1024;
        public const long MaxCharactersInDocument = 16 * 1024 * 1024;

        public static XmlReaderSettings Create(DtdProcessing dtdProcessing = DtdProcessing.Ignore)
        {
            return new XmlReaderSettings
            {
                DtdProcessing = dtdProcessing,
                ValidationType = ValidationType.None,
                IgnoreComments = true,
                XmlResolver = null,
                MaxCharactersFromEntities = MaxCharactersFromEntities,
                MaxCharactersInDocument = MaxCharactersInDocument
            };
        }
    }
}
