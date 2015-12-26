using Parse;
using ReGroup.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReGroup.utility
{
    public class FBFriendComparator : IEqualityComparer<FBFriend>
    {

        public bool Equals(FBFriend x, FBFriend y)
        {
            if (x == y) // same instance or both null
                return true;
            if (x == null || y == null) // either one is null but not both
                return false;

            return x.Id == y.Id;
        }


        public int GetHashCode(FBFriend fbfriend)
        {
            return 17 * 23 + fbfriend.Id.GetHashCode();
        }

    }
}
