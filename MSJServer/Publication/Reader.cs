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
            Account? account = GetLoggedInAccount(context);
            bool isEditor = false;
            bool isOwner = false;
            if (account != null)
            {
                isEditor = account.Permissions >= Permissions.Editor;
                isOwner = account.Name.Equals(article.Author);
            }
            string content = File.ReadAllText(article.PublishStatus == PublishStatus.Published ? "templates/reader.html" : "templates/reader_unpub.html");
            content = content.Replace("{TITLE}", article.Title);
            content = content.Replace("{AUTHOR}", article.Author);
            content = content.Replace("{UPLOADTIME}", article.UploadTime.ToLongDateString());
            content = content.Replace("{BODY}", article.Body);

            content = content.Replace("{COMMENTS}", string.Join("", article.LoadComments(article.PublishStatus == PublishStatus.Published).Select((comment) => comment.ToHTML())));

            if (article.PublishStatus == PublishStatus.UnderReview)
                content = content.Replace("{PUBLISHSTAT}", "<div class=\"alert alert-warning\">This work is currently under review.</div>");
            else if (article.PublishStatus == PublishStatus.Rejected)
            {
                content = content.Replace("{PUBLISHSTAT}", "<div class=\"alert alert-danger\">This work has been rejected by the editing team. To the author, please revise you're work again.</div>");
            }
            else if (article.PublishStatus == PublishStatus.Revised)
            {
                content = content.Replace("{PUBLISHSTAT}", $"<div class=\"alert alert-secondary\">This work has been already been revised by the author. <a href=\"/article?id={article.NextRevision}\">Click here to read the next revision</a>.</div>");
                content = content.Replace("{BUTTONS}", string.Empty);
            }
            else
                content = content.Replace("{PUBLISHTIME}", article.PublishTime.ToLongDateString());
            if (article.PreviousRevision != Guid.Empty)
                content = content.Replace("{PREVREV}", $"<a href = \"/article?id={article.PreviousRevision}\" class=\"btn btn-outline-secondary\">Previous Revision</a>");
            else
                content = content.Replace("{PREVREV}", string.Empty);
            if (isOwner && (isEditor && article.PublishStatus != PublishStatus.Rejected))
                content = content.Replace("{BUTTONS}", "<div><a href=\"/editor?op=publish&id={ARTICLEID}\" class=\"btn btn-outline-success mx-1\">Aprove Article for Publication</a><a href=\"/editor?op=reject&id={ARTICLEID}\" class=\"btn btn-outline-danger\">Reject Article for Publication</a></div><a href=\"/revise_edit?id={ARTICLEID}\" class=\"btn btn-outline-secondary\">Revise this Article</a>");
            if (isOwner)
                content = content.Replace("{BUTTONS}", "<a href=\"/revise_edit?id={ARTICLEID}\" class=\"btn btn-outline-secondary\">Revise this Article</a>");
            if (isEditor)
            {
                content = content.Replace("{CHECK}", "<input class=\"form-check-label\" type=\"checkbox\" id=\"revise\" name=\"revise\" value=\"yes\">Request Revision");
                if (article.PublishStatus != PublishStatus.Rejected)
                    content = content.Replace("{BUTTONS}", "<div><a href=\"/editor?op=publish&id={ARTICLEID}\" class=\"btn btn-outline-success\">Aprove Article for Publication</a><a href=\"/editor?op=reject&id={ARTICLEID}\" class=\"btn btn-outline-danger\">Reject Article for Publication</a></div>");
            } else
            {
                content = content.Replace("{CHECK}", string.Empty);
            }
            if (!isEditor && !isOwner)
                content = content.Replace("{BUTTONS}", string.Empty);
            content = content.Replace("{ARTICLEID}", article.Id.ToString());
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
            if (account == null)
            {
                RespondError(context, "Failed to Upload Article", "You must be logged in to upload an article.");
                return;
            }
            else if (account.ShouldVerify)
            {
                RedirectToVerify(context, "Verify your account before uploading articles.");
            }

            Article article = new Article(Guid.NewGuid(), articleInfo["title"], articleInfo["body"], account.Name, PublishStatus.UnderReview, DateTime.MaxValue, DateTime.Now, Guid.Empty, Guid.Empty);
            article.Save();
            Redirect(context, $"/article?id={article.Id}");
        }

        private void HandleReviseArticle(HttpListenerContext context)
        {
            Dictionary<string, string> articleInfo = context.Request.GetPOSTData();
            Guid id = Guid.Parse(articleInfo["id"]);
            if (!Article.Exists(id))
            {
                RespondError(context, $"Failed to Revise Article.", $"Are you sure article {id} exists?");
                return;
            }

            Article oldArticle = Article.FromFile(id);
            Account? account = GetLoggedInAccount(context);
            if (account == null)
            {
                RespondError(context, "Failed to Revise Article.", "You must be logged in to revise an article.");
                return;
            }
            else if (account.ShouldVerify)
            {
                RedirectToVerify(context, "Verify your account before revising an article.");
                return;
            }

            Article? newArticle = oldArticle.Revise(account, articleInfo["body"]);
            if (newArticle == null)
            {
                RespondError(context, "Failed to Revise Article.", "You are not the author of the article, nor are you an editor.", "This article has already been published, or revised by the author.");
                return;
            }

            newArticle.Save();
            Redirect(context, $"/article?id={newArticle.Id}");
        }

        private void HandleRevisionEditor(HttpListenerContext context)
        {
            Dictionary<string, string> articleInfo = context.Request.GetGETData();
            Guid id = Guid.Parse(articleInfo["id"]);
            if (!Article.Exists(id))
            {
                RespondError(context, "Failed to open revision editor.", $"Are you sure article {id} exists?");
                return;
            }
            Article article = Article.FromFile(id);

            string content = File.ReadAllText("templates/revise.html");
            content = content.Replace("{TITLE}", article.Title);
            content = content.Replace("{BODY}", article.Body);
            content = content.Replace("{ARTICLEID}", article.Id.ToString());

            Respond202(context, content);
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
            if (article.PublishStatus != PublishStatus.UnderReview)
            {
                RespondError(context, "Failed to Perform Editing Operation.", $"An editing decision regarding this article has already been made. It cannot be undone.");
                return;
            }

            Account? editor = GetLoggedInAccount(context);
            if (editor == null)
            {
                RespondError(context, "Failed to Perform Editing Operation", "You must log in to perform an editing operation.");
                return;
            }
            if (editor.Permissions < Permissions.Editor)
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
