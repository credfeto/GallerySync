using System.Collections.Generic;
using System.Linq;
using Twaddle.Gallery.ObjectModel;

namespace BuildSiteIndex
{
    internal static class ItemUpdateHelpers
    {
        public static bool AreSame(GalleryItem oldItem, GalleryItem item)
        {
            return oldItem == item;
        }


        public static bool CollectionEquals(List<ImageSize> lhs, List<ImageSize> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, null) && !ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (!ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(lhsItem => rhs.FirstOrDefault(candidate => candidate == lhsItem)).All(rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<GalleryEntry> lhs, List<GalleryEntry> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, null) && !ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (!ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(lhsItem => rhs.FirstOrDefault(candidate => candidate == lhsItem)).All(rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<GalleryChildItem> lhs, List<GalleryChildItem> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, null) && !ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (!ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(lhsItem => rhs.FirstOrDefault(candidate => candidate == lhsItem)).All(rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<PhotoMetadata> lhs, List<PhotoMetadata> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, null) && !ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (!ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(lhsItem => rhs.FirstOrDefault(candidate => candidate == lhsItem)).All(rhsItem => rhsItem != null);
        }

        public static bool CollectionEquals(List<string> lhs, List<string> rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }

            if (ReferenceEquals(lhs, null) && !ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (!ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
            {
                return false;
            }

            if (lhs.Count != rhs.Count)
            {
                return false;
            }

            return lhs.Select(lhsItem => rhs.FirstOrDefault(candidate => candidate == lhsItem)).All(rhsItem => rhsItem != null);
        }
    }
}