namespace Vostok.ZooKeeper.Client.Helpers
{
    internal static class PathHelper
    {
        public static string[] SplitPath(string path)
        {
            return path.Trim('/').Split('/');
        }
    }
}