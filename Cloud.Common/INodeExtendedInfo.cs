namespace Azi.Cloud.Common
{
    public interface INodeExtendedInfo
    {
        string Id { get; }

        bool CanShareReadOnly { get; }

        bool CanShareReadWrite { get; }
    }
}