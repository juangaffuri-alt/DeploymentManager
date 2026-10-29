namespace PasadorScript.DTO.Graph
{
    public class Avatar
    {
        public string href { get; set; }
    }

    public class Links
    {
        public Self self { get; set; }
        public Memberships memberships { get; set; }
        public MembershipState membershipState { get; set; }
        public StorageKey storageKey { get; set; }
        public Avatar avatar { get; set; }
    }

    public class Memberships
    {
        public string href { get; set; }
    }

    public class MembershipState
    {
        public string href { get; set; }
    }

    public class UsersResponse
    {
        public int count { get; set; }
        public List<UserInfo> value { get; set; }
    }

    public class Self
    {
        public string href { get; set; }
    }

    public class StorageKey
    {
        public string href { get; set; }
    }

    public class UserInfo
    {
        public string subjectKind { get; set; }
        public string metaType { get; set; }
        public string directoryAlias { get; set; }
        public string domain { get; set; }
        public string principalName { get; set; }
        public string mailAddress { get; set; }
        public string origin { get; set; }
        public string originId { get; set; }
        public string displayName { get; set; }
        public Links _links { get; set; }
        public string url { get; set; }
        public string descriptor { get; set; }
    }

    public class Membership
    {
        public int count { get; set; }
        public List<Member> value { get; set; }
    }

    public class Member
    {
        public string containerDescriptor { get; set; }
        public string memberDescriptor { get; set; }
    }
}
