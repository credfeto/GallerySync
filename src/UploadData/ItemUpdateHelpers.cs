using System;
using System.Collections.Generic;
using System.Linq;
using FileNaming;
using ObjectModel;

namespace UploadData
{
    public static class ItemUpdateHelpers
    {
        public static bool AreSame(GalleryItem oldItem, GalleryItem other)
        {
            if (!StringComparer.InvariantCulture.Equals(oldItem.Path.AsEmpty(), other.Path.AsEmpty()))
            {
                Console.WriteLine(format: " >> Path Different ({0}) vs ({1})", oldItem.Path, other.Path);

                return false;
            }

            if (oldItem.OriginalAlbumPath.AsEmpty() != other.OriginalAlbumPath.AsEmpty())
            {
                Console.WriteLine(format: " >> OriginalAlbumPath Different ({0})", oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCulture.Equals(oldItem.Title.AsEmpty(), other.Title.AsEmpty()))
            {
                Console.WriteLine(format: " >> Title Different ({0})", oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCulture.Equals(oldItem.Description.AsEmpty(), other.Description.AsEmpty()))
            {
                Console.WriteLine(format: " >> Description Different ({0})", oldItem.Path);

                return false;
            }

            if (!DatesEqual(oldItem.DateCreated, other.DateCreated))
            {
                Console.WriteLine(format: " >> DateCreated Different ({0})", oldItem.Path);

                return false;
            }

            if (!DatesEqual(oldItem.DateUpdated, other.DateUpdated))
            {
                Console.WriteLine(format: " >> DaterUpdated Different ({0})", oldItem.Path);

                return false;
            }

            if (oldItem.Location != other.Location)
            {
                Console.WriteLine(format: " >> Location Different ({0})", oldItem.Path);

                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(oldItem.Type, other.Type))
            {
                Console.WriteLine(format: " >> Type Different ({0})", oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(oldItem.First, other.First))
            {
                Console.WriteLine(format: " >> First Different ({0})", oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(oldItem.Previous, other.Previous))
            {
                Console.WriteLine(format: " >> Previous Different ({0})", oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(oldItem.Next, other.Next))
            {
                Console.WriteLine(format: " >> Next Different ({0})", oldItem.Path);

                return false;
            }

            if (!ReferenceItemEquals(oldItem.Last, other.Last))
            {
                Console.WriteLine(format: " >> Last Different ({0})", oldItem.Path);

                return false;
            }

            if (!CollectionEquals(oldItem.ImageSizes, other.ImageSizes))
            {
                Console.WriteLine(format: " >> ImageSizes Different ({0})", oldItem.Path);

                return false;
            }

            if (!CollectionEquals(oldItem.Metadata, other.Metadata))
            {
                Console.WriteLine(format: " >> Metadata Different ({0})", oldItem.Path);

                return false;
            }

            if (!CollectionEquals(oldItem.Keywords, other.Keywords))
            {
                Console.WriteLine(format: " >> Keywords Different ({0})", oldItem.Path);

                return false;
            }

            if (!CollectionEquals(oldItem.Children, other.Children))
            {
                Console.WriteLine(format: " >> Children Different ({0})", oldItem.Path);

                return false;
            }

            if (!CollectionEquals(oldItem.Breadcrumbs, other.Breadcrumbs))
            {
                Console.WriteLine(format: " >> Breadcrumbs Different ({0})", oldItem.Path);

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
            if (ReferenceEquals(lhs, objB: null) && ReferenceEquals(rhs, objB: null))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
            {
                return false;
            }

            return lhs.Path == rhs.Path;
        }

        private static bool ReferenceItemEquals(GalleryEntry lhs, GalleryEntry rhs)
        {
            if (ReferenceEquals(lhs, objB: null) && ReferenceEquals(rhs, objB: null))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
            {
                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(lhs.Path, rhs.Path))
            {
                return false;
            }

            if (!StringComparer.InvariantCultureIgnoreCase.Equals(lhs.Title, rhs.Title))
            {
                return false;
            }

            return true;
        }

        public static bool CollectionEquals(List<ImageSize> lhs, List<ImageSize> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
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

        public static bool CollectionEquals(List<GalleryEntry> lhs, List<GalleryEntry> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => ReferenceItemEquals(candidate, lhsItem)))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<GalleryChildItem> lhs, List<GalleryChildItem> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(selector: lhsItem => rhs.FirstOrDefault(predicate: candidate => ReferenceItemEquals(candidate, lhsItem)))
                      .All(predicate: rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<PhotoMetadata> lhs, List<PhotoMetadata> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
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

        public static bool CollectionEquals(List<string> lhs, List<string> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, objB: null))
            {
                return false;
            }

            if (ReferenceEquals(rhs, objB: null))
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