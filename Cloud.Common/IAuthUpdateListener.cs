namespace Azi.Cloud.Common
{
    public interface IAuthUpdateListener
    {
        void OnAuthUpdated(IHttpCloud sender, string authinfo);
    }
}