namespace Azi.Tools
{
    using System.Threading.Tasks;

    public interface IBackgroundWorker<in TParam>
    {
        Task Run(TParam param);
    }

    public interface IBackgroundWorker
    {
        Task Run();
    }
}
