using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Gallery.FileNaming;
using Credfeto.Gallery.ObjectModel;

namespace Credfeto.Gallery.UploadData
{
    public static class ItemUpdateHelpers
    {
        public static bool AreSame(GalleryItem oldItem, GalleryItem other)
        {
            if (!StringComparer.InvariantCulture.Equals(oldItem.Path.AsEmpty(), other.Path.AsEmpty()))
            {
                Console.WriteLine(format: " >> Path Different ({0}) vs ({1})", arg0: oldItem.Path, arg1: other.Path);

                return false;
            }

            if (oldItem.OriginalAlbumPath.AsEmpty() != other.OriginalAlbumPath.AsEmpty())
            {
                Console.WriteLine(format: " >> OriginalAlbumPath Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCulture.Equals(oldItem.Title.AsEmpty(), other.Title.AsEmpty()))
            {
                Console.WriteLine(format: " >> Title Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCulture.Equals(oldItem.Description.AsEmpty(), other.Description.AsEmpty()))
            {
                Console.WriteLine(format: " >> Description Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!DatesEqual(lhs: oldItem.DateCreated, rhs: other.DateCreated))
            {
                Console.WriteLine(format: " >> DateCreated Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!DatesEqual(lhs: oldItem.DateUpdated, rhs: other.DateUpdated))
            {
                Console.WriteLine(format: " >> DaterUpdated Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (oldItem.Location != other.Location)
            {
                Console.WriteLine(format: " >> Location Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: oldItem.Type, y: other.Type))
            {
                Console.WriteLine(format: " >> Type Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(lhs: oldItem.First, rhs: other.First))
            {
                Console.WriteLine(format: " >> First Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(lhs: oldItem.Previous, rhs: other.Previous))
            {
                Console.WriteLine(format: " >> Previous Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(lhs: oldItem.Next, rhs: other.Next))
            {
                Console.WriteLine(format: " >> Next Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(lhs: oldItem.Last, rhs: other.Last))
            {
                Console.WriteLine(format: " >> Last Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!CollectionEquals(lhs: oldItem.ImageSizes, rhs: other.ImageSizes))
            {
                Console.WriteLine(format: " >> ImageSizes Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!CollectionEquals(lhs: oldItem.Metadata, rhs: other.Metadata))
            {
                Console.WriteLine(format: " >> Metadata Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!CollectionEquals(lhs: oldItem.Keywords, rhs: other.Keywords))
            {
                Console.WriteLine(format: " >> Keywords Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!CollectionEquals(lhs: oldItem.Children, rhs: other.Children))
            {
                Console.WriteLine(format: " >> Children Different ({0})", arg0: oldItem.Path);

                return false;
            }

            if (!CollectionEquals(lhs: oldItem.Breadcrumbs, rhs: other.Breadcrumbs))
            {
                Console.WriteLine(format: " >> Breadcrumbs Different ({0})", arg0: oldItem.Path);

                return false;
            }

            return true;
        }

        private static bool DatesEqual(DateTime lhs, DateTime rhs)
        {
            return lhs.Year == rhs.Year && lhs.Month == rhs.Month && lhs.Day == rhs.Day && lhs.Hour == rhs.Hour && lhs.Minute == rhs.Minute;
        }

        private static bool ReferenceItemEquals(GalleryChildItem lhs, GalleryChildItem rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: null) && ReferenceEquals(objA: rhs, objB: null))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            return lhs.Path == rhs.Path;
        }

        private static bool ReferenceItemEquals(GalleryEntry lhs, GalleryEntry rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: null) && ReferenceEquals(objA: rhs, objB: null))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: lhs.Path, y: rhs.Path))
            {
                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(x: lhs.Title, y: rhs.Title))
            {
                return false;
            }

            return true;
        }

        public static bool CollectionEquals(IReadOnlyList<ImageSize> lhs, IReadOnlyList<ImageSize> rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: rhs))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => candidate == lhsItem))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(IReadOnlyList<GalleryEntry> lhs, IReadOnlyList<GalleryEntry> rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: rhs))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => ReferenceItemEquals(lhs: candidate, rhs: lhsItem)))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(IReadOnlyList<GalleryChildItem> lhs, IReadOnlyList<GalleryChildItem> rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: rhs))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => ReferenceItemEquals(lhs: candidate, rhs: lhsItem)))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(IReadOnlyList<PhotoMetadata> lhs, IReadOnlyList<PhotoMetadata> rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: rhs))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => candidate == lhsItem))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(IReadOnlyList<string> lhs, IReadOnlyList<string> rhs)
        {
            if (ReferenceEquals(objA: lhs, objB: rhs))
            {
                return true;
            }

            if (ReferenceEquals(objA: lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(objA: rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => candidate == lhsItem))
                      .All(predicate: rhsItem => rhsItem != null);
        }
    }
}