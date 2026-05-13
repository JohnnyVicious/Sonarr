using System;
using System.Xml;

#if !NET10_0_OR_GREATER
[assembly: CLSCompliant(false)]
#endif

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
