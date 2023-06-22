using System.Net;
using System.Text;
using MSJServer.HTTP;

namespace MSJServer
{
    public class Comment
    {
        public static List<Comment> LoadComments(Guid discussionGuid, bool excludeRevisions)
        {
            if (!File.Exists($"comments/{discussionGuid}.dat"))
                return new();
            using(FileStream stream = new FileStream($"comments/{discussionGuid}.dat", FileMode.Open, FileAccess.Read))
            using(BinaryReader reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                List<Comment> comments = new(count);
                for (int i = 0; i < count; i++)
                {
                    Comment loadedComment = new Comment(reader);
                    if(!(excludeRevisions && loadedComment.RevisionRequested))
                        comments.Add(loadedComment);
                }
                return comments;
            }
        }

        public static void MakeComment(Guid discussionGuid, Comment comment)
        {
            bool exist = true;
            if (!File.Exists($"comments/{discussionGuid}.dat"))
            {
                File.Create($"comments/{discussionGuid}.dat").Close();
                exist = false;
            }
            using(FileStream stream = new FileStream($"comments/{discussionGuid}.dat", FileMode.Open, FileAccess.ReadWrite))
            {
                int commentCount = 1;
                if (exist)
                {
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                        commentCount = reader.ReadInt32();
                    stream.Position = 0;
                    commentCount++;
                }
                using(BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(commentCount);
                    stream.Position = stream.Length;
                    comment.WriteBack(writer);
                }
            }
        }

        public string Sender { get; private set; }
        public string Content { get; private set; }
        public DateTime Created { get; private set; }
        public bool RevisionRequested { get; private set; }

        public Comment(string sender, string content, bool revisionRequested, DateTime created)
        {
            Sender = sender;
            Content = content;
            Created = created;
            RevisionRequested = revisionRequested;
        }

        public Comment(BinaryReader reader) : this(reader.ReadString(), reader.ReadString(), reader.ReadBoolean(), new DateTime(reader.ReadInt64()))
        {

        }

        public void WriteBack(BinaryWriter writer)
        {
            writer.Write(Sender);
            writer.Write(Content);
            writer.Write(RevisionRequested);
            writer.Write(Created.Ticks);
        }

        public string ToHTML() => $"<div><a href=\"/userinfo?username={Sender}\">{Sender}</a><a class=\"text-muted\"> • {Created.ToShortTimeString()}</a><a><span class=\"badge badge-primary mx-1\">{(RevisionRequested ? "Revision Requested" : string.Empty)}</span></a></div>{Content}<hr>";
    }

    partial class Server
    {
        private void HandleCommentRequest(HttpListenerContext context)
        {
            Dictionary<string, string> commentInfo = context.Request.GetPOSTData();
            Guid id = Guid.Parse(commentInfo["id"]);
            bool requestRevision = commentInfo.ContainsKey("revise") && commentInfo["revise"] == "yes";

            if (!Article.Exists(id))
            {
                RespondError(context, $"Failed to Make Comment", $"Are you sure article {id} exists?");
                return;
            }

            Article article = Article.FromFile(id);
            Account? account = GetLoggedInAccount(context);
            if (account == null)
            {
                RespondError(context, "Failed to Make Comment", "You must be logged in to comment.");
                return;
            }
            else if(requestRevision && account.Permissions < Permissions.Editor)
            {
                RespondError(context, "Failed to Make Comment", "Only editors can request revisions.");
                return;
            }
            else if (account.ShouldVerify)
            {
                RedirectToVerify(context, "Verify you account before posting comments.");
                return;
            }

            article.MakeComment(new Comment(account.Name, commentInfo["msg"], requestRevision, DateTime.Now));
            if (accounts.ContainsKey(article.Author))
            {
                Account author = accounts[article.Author];
                if (requestRevision)
                    Notification.MakeNotification(author, $"Revision Requested on {article.Title}.", $"An editor, {account.Name}, has requested that you revise your article, {article.Title}. Here are {account.Name}'s comments:\n{commentInfo["msg"]}", Notification.Serverity.ShouldResolve, $"/article?id={article.Id}");
                else
                    Notification.MakeNotification(author, $"New Comment on {article.Title} from {account.Name}", commentInfo["msg"], Notification.Serverity.CanIgnore, $"/article?id={article.Id}");
            }
            Redirect(context, $"/article?id={id}");
        }
    }
}
