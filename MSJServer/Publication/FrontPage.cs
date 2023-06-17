using MSJServer.HTTP;
using System.Net;
using System.Text;

namespace MSJServer
{
    partial class Server
    {
        private void HandleFrontPageAccess(HttpListenerContext context)
        {
            Dictionary<string, string> queryInfo = context.Request.GetGETData();

            Account? account = GetLoggedInAccount(context);
            string content;
            bool isEditor = false;
            if (account == null)
                content = File.ReadAllText("templates/index.html");
            else
            {
                content = File.ReadAllText("templates/index_signin.html");
                content = content.Replace("{USERNAME}", account.Name);
                isEditor = (account.Permissions >= Permissions.Editor);
            }
            content = content.Replace("{EXTFLAGS}", queryInfo.ContainsKey("unpub") ? "&unpub=yes" : string.Empty);
            content = content.Replace("{DATE}", DateOnly.FromDateTime(DateTime.Now).ToLongDateString());
            content = content.Replace("{INVFLAGS}", queryInfo.ContainsKey("unpub") ? string.Empty : "&unpub=yes");
            content = content.Replace("{SECTION}", queryInfo.ContainsKey("unpub") ? "Published Works" : "Unpublished Works");

            Guid[] publishedArticles = Article.GetPublishedArticles(queryInfo.ContainsKey("unpub"));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < publishedArticles.Length; i++)
            {
                if ((i + 2) % 5 == 0 || i % 5 == 0)
                {
                    if (i != 0)
                    {
                        builder.Append($"</div>");
                    }
                    builder.Append($"<div class=\"row\">");
                }
                Article article = Article.FromFile(publishedArticles[i]);
                builder.Append($"<div class=\"col-sm\"><div class=\"card mb-3\" style=\"max-width:540px\"><div class=\"row no-gutters\"><div class=\"col-md-4\" style=\"background-color:#34A2A2;\"></div><div class=\"col-md-8\"><div class=\"card-body\">");
                builder.Append($"<h5 class=\"card-title\">{article.Title}</h5>");
                builder.Append($"<p class=\"card-text\">{article.Snippet}...</p>");
                builder.Append($"<a class=\"btn btn-outline-secondary\" href = \"/article?id={article.Id}\">Read More</a>");
                if (isEditor)
                {
                    if (article.PublishStatus == PublishStatus.UnderReview)
                        builder.Append("<br><b class=\"mt-2 badge badge-warning\">Pending</b>");
                }
                if (article.PublishStatus == PublishStatus.Revised)
                    builder.Append("<br><b class=\"mt-2 badge badge-secondary\">Old Revision</b>");
                else if (article.PublishStatus == PublishStatus.Rejected)
                    builder.Append("<br><b class=\"mt-2 badge badge-danger\">Rejected</b>");
                if (article.PublishStatus == PublishStatus.Published)
                    builder.Append($"</div><div class=\"card-footer bg-transparent text-muted\">Written by <a href=\"/userinfo?username={article.Author}\">{article.Author}</a><br>{article.PublishTime}</div></div></div></div></div>");
                else
                    builder.Append($"</div><div class=\"card-footer bg-transparent text-muted\">Written by <a href=\"/userinfo?username={article.Author}\">{article.Author}</a><br>{article.UploadTime}</div></div></div></div></div>");
            }
            builder.Append($"</div>");
            content = content.Replace("{DATA}", builder.ToString());

            Respond202(context, content);
        }
    }
}