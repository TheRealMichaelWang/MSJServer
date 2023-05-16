using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MSJServer
{
    public static class AIEditor
    {
        static Process alpacaProcess;

        static AIEditor()
        {
            alpacaProcess = new Process();
            alpacaProcess.StartInfo.FileName = "alpaca/chat";
            alpacaProcess.StartInfo.UseShellExecute = false;

            alpacaProcess.StartInfo.RedirectStandardError = true;
            alpacaProcess.StartInfo.RedirectStandardOutput = true;
            alpacaProcess.StartInfo.RedirectStandardInput = true;

            alpacaProcess.Start();
        }

        public static bool Revise(Article article)
        {
            lock (alpacaProcess) {
                //wait until alpaca is ready for input
                if (alpacaProcess.StandardOutput.Peek() != '>') {
                    int read;
                    do
                    {
                        if (alpacaProcess.HasExited)
                            return false;

                        read = alpacaProcess.StandardOutput.Read();
                    } while (read != '>');
                }

                //strip html tags from body
                string textBody = Regex.Replace(article.Body, @"<(.|\n)*?>", string.Empty);

                //replace newlines with specific alpaca newline codes
                textBody = textBody.Replace("\r\n", "\\\n");
                textBody = textBody.Replace("\n", "\\\n");

                alpacaProcess.StandardInput.WriteLine(textBody);

                //load standard output
                StringBuilder output = new();
                while(alpacaProcess.StandardOutput.Peek() != '>')
                {
                    if (alpacaProcess.HasExited)
                        return false;

                    int read = alpacaProcess.StandardOutput.Read();
                    if (read != -1)
                        output.Append((char)read);
                }

                article.MakeComment(new Comment("MSJ AI AutoEditor", output.ToString(), true, DateTime.Now));
                return true;
            }
        }
    }
}
