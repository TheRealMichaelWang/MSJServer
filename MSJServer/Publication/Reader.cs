using MSJServer.HTTP;
using System.Net;

namespace MSJServer
{
    partial class Server
    {
        private void OpenArticle(HttpListenerContext context, Guid id)
        {
            if (!Article.Exists(id))
            {
                RespondError(context, $"Failed to load article.", $"Are you sure article {id} exists?");
                return;
            }
            Article article = Article.FromFile(id);
            string content = File.ReadAllText(article.PublishStatus == PublishStatus.Published ? "templates/reader.html" : "templates/reader_unpub.html");
            content = content.Replace("{TITLE}", article.Title);
            content = content.Replace("{AUTHOR}", article.Author);
            content = content.Replace("{UPLOADTIME}", article.UploadTime.ToLongDateString());
            content = content.Replace("{BODY}", article.Body);

            content = content.Replace("{ARTICLEID}", article.Id.ToString());
            if (article.PublishStatus == PublishStatus.UnderReview)
            {
                content = content.Replace("{PUBLISHSTAT}", "This work is currently under review.");
                content = content.Replace("{COMMENTS}", string.Join("<br>", Comment.LoadComments(id, false)));
            }
            else if (article.PublishStatus == PublishStatus.Rejected)
            {
                content = content.Replace("{PUBLISHSTAT}", "This work has been rejected by the editing team. To the author, please revise you're work again.");
                content = content.Replace("{COMMENTS}", string.Join("<br>", Comment.LoadComments(id, false)));
            }
            else
            {
                content = content.Replace("{PUBLISHTIME}", article.PublishTime.ToLongDateString());
                content = content.Replace("{COMMENTS}", string.Join("<br>", Comment.LoadComments(id, true)));
            }
            Respond202(context, content);
        }

        private void HandleReadArticle(HttpListenerContext context)
        {
            Dictionary<string, string> articleInfo = context.Request.GetGETData();
            Guid id = Guid.Parse(articleInfo["id"]);
            OpenArticle(context, id);
        }

        private void HandleUploadArticle(HttpListenerContext context)
        {
            Dictionary<string, string> articleInfo = context.Request.GetPOSTData();
            
            Account? account = GetLoggedInAccount(context);
            if(account == null)
            {
                RespondError(context, "Failed to Upload Article", "You must be logged in to upload an article.");
                return;
            }

            Article article = new Article(Guid.NewGuid(), articleInfo["title"], articleInfo["body"], account.Name, PublishStatus.UnderReview, DateTime.MaxValue, DateTime.Now);
            article.Save();
            Redirect(context, $"/article?id={article.Id}");
        }

        private void HandleEditorRequest(HttpListenerContext context)
        {
            Dictionary<string, string> articleInfo = context.Request.GetGETData();
            Guid id = Guid.Parse(articleInfo["id"]);
            if (!Article.Exists(id))
            {
                RespondError(context, "Failed to Perform Editing Operation.", $"Are you sure article {id} exists?");
                return;
            }
            Article article = Article.FromFile(id);
            if(article.PublishStatus != PublishStatus.UnderReview)
            {
                RespondError(context, "Failed to Perform Editing Operation.", $"An editing decision regarding this article has already been made. It cannot be undone.");
                return;
            }

            Account? editor = GetLoggedInAccount(context);
            if(editor == null)
            {
                RespondError(context, "Failed to Perform Editing Operation", "You must log in to perform an editing operation.");
                return;
            }
            if(editor.Permissions < Permissions.Editor)
            {
                RespondError(context, "Failed to Perform Editing Operation", "You are not an editor.");
                return;
            }

            switch (articleInfo["op"])
            {
                case "p":
                case "pub":
                case "publish":
                    article.Publish();
                    Redirect(context, $"/article?id={article.Id}");
                    break;
                case "rej":
                case "reject":
                    article.Reject();
                    Redirect(context, $"/article?id={article.Id}");
                    break;
                default:
                    RespondError(context, "Failed to Perform Editing Operation", $"Unrecognized editor operation {articleInfo["op"]}.");
                    break;
            }
        }
    }
}
