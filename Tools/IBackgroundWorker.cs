namespace Azi.Tools
{
    using System.Threading.Tasks;

    public interface IBackgroundWorker<P>
    {
        Task Run(P param);
    }

    public interface IBackgroundWorker
    {
        Task Run();
    }
}
