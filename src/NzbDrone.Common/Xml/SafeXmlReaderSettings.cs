using System.Xml; // NOSONAR S3990: This legacy assembly needs an API-wide CLS migration.

namespace NzbDrone.Common.Xml
{
    public static class SafeXmlReaderSettings
    {
        public static long MaxCharactersFromEntities => 1024 * 1024;
        public static long MaxCharactersInDocument => 16 * 1024 * 1024;

        public static XmlReaderSettings Create()
        {
            return Create(DtdProcessing.Ignore);
        }

        public static XmlReaderSettings Create(DtdProcessing dtdProcessing)
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
