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

            DateOnly day = queryInfo.ContainsKey("day") ? DateOnly.FromDayNumber(int.Parse(queryInfo["day"])) : DateOnly.FromDateTime(DateTime.Now);
            DateOnly nextDay = day.AddDays(1);
            DateOnly prevDay = day.AddDays(-1); 

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
            content = content.Replace("{DATE}", day.ToLongDateString());
            content = content.Replace("{TODAYTICKS}", day.DayNumber.ToString());
            content = content.Replace("{TOMMROW}", nextDay.ToShortDateString());
            content = content.Replace("{TOMMOROWTICKS}", nextDay.DayNumber.ToString());
            content = content.Replace("{YESTARDAY}", prevDay.ToShortDateString());
            content = content.Replace("{YESTARDAYTICKS}", prevDay.DayNumber.ToString());
            content = content.Replace("{INVFLAGS}", queryInfo.ContainsKey("unpub") ? string.Empty : "&unpub=yes");
            content = content.Replace("{SECTION}", queryInfo.ContainsKey("unpub") ? "Published Works" : "Unpublished Works");

            Guid[] publishedArticles = Article.GetPublishedArticles(day, queryInfo.ContainsKey("unpub"));
            if (publishedArticles.Length == 0)
                content = content.Replace("{DATA}", $"As of yet, no articles were published on {day.ToLongDateString()}.");
            else
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("<hr>");
                foreach(Guid articleGuid in publishedArticles)
                {
                    Article article = Article.FromFile(articleGuid);
                    builder.Append($"<h3><a href = \"/article?id={article.Id}\">{article.Title}</a></h3>");
                    builder.Append($"<p>{article.Snippet}...</p>");
                    if (article.PublishStatus == PublishStatus.UnderReview && isEditor)
                        builder.Append("<br><b>Editor Attention Required!</b>");
                    builder.Append("<hr>");
                }
                content = content.Replace("{DATA}", builder.ToString());
            }
            Respond202(context, content);
        }
    }
}
