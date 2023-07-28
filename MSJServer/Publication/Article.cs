using System.Text.RegularExpressions;

namespace MSJServer
{
    public enum PublishStatus
    {
        Published,
        UnderReview,
        Revised,
        Rejected
    }

    public sealed class Article
    {
        public static readonly byte CurrentVersion = 1;

        public static bool Exists(Guid id) => File.Exists("articles/" + id.ToString());

        public static Article FromFile(Guid id)
        {
            using (FileStream stream = new FileStream("articles/" + id.ToString(), FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
                return FromReader(reader, id);
        }

        public static Guid[] GetArticles(bool excludeUnpublished, int page, int pageSize)
        {
            string[] paths = Directory.GetFiles("articles");
            List<Guid> validArticles = new List<Guid>(10);

            int toLoad = (page - 1) * pageSize;
            int loaded = 0;
            foreach (string path in paths)
            {
                //32 digits, 4 dashes
                Article article = FromFile(Guid.Parse(path.Substring(path.Length - 36, 36)));
                loaded++;

                if (excludeUnpublished)
                {
                    if (article.PublishStatus == PublishStatus.Published)
                        continue;

                    if (loaded > toLoad)
                    {
                        validArticles.Add(article.Id);

                        if (loaded == toLoad + pageSize)
                            break;
                    }

                }
                else if (article.PublishStatus == PublishStatus.Published)
                {
                    if (loaded > toLoad)
                    {
                        validArticles.Add(article.Id);

                        if (loaded == toLoad + pageSize)
                            break;
                    }
                }
            }

            return validArticles.ToArray();
        }

        private static Article FromReader(BinaryReader reader, Guid id)
        {
            byte articleVersion = reader.ReadByte();
            switch (articleVersion)
            {
                case 0:
                    Logger.Log(Logger.Severity.Information, $"Upgrading article {id} from version 0.");
                    return new Article(id, reader.ReadString(), reader.ReadString(), reader.ReadString(), (PublishStatus)reader.ReadByte(), new DateTime(reader.ReadInt64()), new DateTime(reader.ReadInt64()), Guid.Empty, Guid.Empty);
                case 1:
                    Logger.Log(Logger.Severity.Information, $"Upgrading article {id} from version 1.");
                    return new Article(id, reader.ReadString(), reader.ReadString(), reader.ReadString(), (PublishStatus)reader.ReadByte(), new DateTime(reader.ReadInt64()), new DateTime(reader.ReadInt64()), new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16)));
                default:
                    Logger.Log(Logger.Severity.Alert, $"Unable to load article {id} because of invalid version {articleVersion}.");
                    throw new InvalidOperationException($"Invalid article version {articleVersion}, for article {id}.");
            }
        }

        public Guid Id { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string Author { get; private set; }

        public string Snippet
        {
            get
            {
                string nohtml = Regex.Replace(Body, "<.*?>", string.Empty);
                return nohtml.Substring(0, Math.Min(nohtml.Length, 150));
            }
        }

        public Guid PreviousRevision { get; private set; }
        public Guid NextRevision { get; private set; }

        public DateTime PublishTime { get; private set; }
        public DateTime UploadTime { get; private set; }
        public PublishStatus PublishStatus { get; private set; }

        public Article(Guid id, string title, string body, string author, PublishStatus publishStatus, DateTime publishTime, DateTime uploadTime, Guid previousRevision, Guid nextRevision)
        {
            Id = id;
            Title = title;
            Body = body;
            Author = author;
            PublishStatus = publishStatus;
            PublishTime = publishTime;
            UploadTime = uploadTime;
            PreviousRevision = previousRevision;
            NextRevision = nextRevision;
        }

        private void WriteTo(BinaryWriter writer)
        {
            writer.Write(CurrentVersion);
            writer.Write(Title);
            writer.Write(Body);
            writer.Write(Author);
            writer.Write((byte)PublishStatus);
            writer.Write(PublishTime.Ticks);
            writer.Write(UploadTime.Ticks);
            writer.Write(PreviousRevision.ToByteArray());
            writer.Write(NextRevision.ToByteArray());
        }

        public void Save()
        {
            using (FileStream stream = new FileStream("articles/" + Id.ToString(), FileMode.OpenOrCreate, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
                WriteTo(writer);
        }

        public void Publish()
        {
            if (PublishStatus != PublishStatus.UnderReview)
                throw new InvalidOperationException();

            PublishStatus = PublishStatus.Published;
            PublishTime = DateTime.Now;
            Save();

            Logger.Log(Logger.Severity.Information, $"Article {Title} (Id {Id}) has been published.", Author);
        }

        public void Reject()
        {
            if (PublishStatus != PublishStatus.UnderReview)
                throw new InvalidOperationException();

            PublishStatus = PublishStatus.Rejected;
            Save();

            Logger.Log(Logger.Severity.Information, $"Article {Title} (Id {Id}) has been rejected.", Author);
        }

        public Article? Revise(Account newAuthor, string body)
        {
            if (PublishStatus == PublishStatus.Published || PublishStatus == PublishStatus.Revised)
                return null;

            if (newAuthor.Name == Author)
            {
                Article revised = new Article(Guid.NewGuid(), Title, body, Author, PublishStatus.UnderReview, DateTime.MaxValue, DateTime.Now, Id, Guid.Empty);
                PublishStatus = PublishStatus.Revised;
                NextRevision = revised.Id;
                Title = $"Unrevised {Title}, dated {UploadTime.ToShortDateString()}";
                Save();

                Logger.Log(Logger.Severity.Information, $"Article {Title}(Id {Id}) was revised by author.", Author);
                return revised;
            }
            else if (newAuthor.Permissions >= Permissions.Editor)
            {
                Logger.Log(Logger.Severity.Information, $"Article {Title}(Id {Id}, Author {Author}) was revised by an editor.", newAuthor.Name);
                return new Article(Guid.NewGuid(), $"{Title} - Revised by {newAuthor.Name}", body, newAuthor.Name, PublishStatus.UnderReview, DateTime.MaxValue, DateTime.Now, Id, Guid.Empty);
            }
            return null;
        }

        public List<Comment> LoadComments(bool excludeRevisions)
        {
            if (PreviousRevision != Guid.Empty && Exists(PreviousRevision))
            {
                Article article = FromFile(PreviousRevision);
                return article.LoadComments(excludeRevisions);
            }
            return Comment.LoadComments(Id, excludeRevisions);
        }

        public void MakeComment(Comment comment)
        {
            if (PreviousRevision != Guid.Empty && Exists(PreviousRevision))
            {
                Article article = FromFile(PreviousRevision);
                article.MakeComment(comment);
            }
            else
                Comment.MakeComment(Id, comment);
        }
    }
}