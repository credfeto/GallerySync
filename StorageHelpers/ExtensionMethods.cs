using System.Collections.Generic;
using Raven.Abstractions.Data;
using Raven.Client;

namespace StorageHelpers
{
    public static class ExtensionMethods
    {
        public static IEnumerable<TObjectType> GetAll<TObjectType>(this IDocumentSession session)
            where TObjectType : class
        {
            using (
                IEnumerator<StreamResult<object>> enumerator =
                    session.Advanced.Stream<object>(fromEtag: Etag.Empty,
                                                    start: 0,
                                                    pageSize: int.MaxValue))
                while (enumerator.MoveNext())
                {
                    var file = enumerator.Current.Document as TObjectType;
                    if (file != null)
                    {
                        yield return file;
                    }
                }
        }
    }
}