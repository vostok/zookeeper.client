using System.Text;

namespace Vostok.ZooKeeper.Client.Helpers
{
    // See https://vostok.gitbook.io/logging/concepts/syntax/message-templates
    internal class VostokMessageTemplateEscaper
    {
        public static string Escape(string template)
        {
            var stringBuilder = new StringBuilder();

            foreach (var chr in template)
            {
                if (chr == '{' || chr == '}')
                    stringBuilder.Append(chr);
                stringBuilder.Append(chr);
            }

            return stringBuilder.ToString();
        }
    }
}